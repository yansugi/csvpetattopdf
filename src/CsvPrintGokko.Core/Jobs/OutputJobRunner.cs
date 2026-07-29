using System.Collections.Concurrent;
using System.Globalization;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Output;
using CsvPrintGokko.Core.Pdf;
using PdfSharp.Pdf;

namespace CsvPrintGokko.Core.Jobs;

/// <summary>
/// CSVの全行分のPDF出力(単票: 結合/個別、一覧表: 常に1つのPDF)をバックグラウンドTaskで実行し、
/// 進捗をポーリングで参照できるようにする。
/// 単一プロセス・単一ユーザー向けの規模感のため、ジョブ状態はインメモリのConcurrentDictionaryで管理する。
/// </summary>
public sealed class OutputJobRunner
{
    private readonly ConcurrentDictionary<Guid, OutputJobStatus> _jobs = new();
    private readonly PdfComposerService _composer = new();
    private readonly FileNamePatternService _fileNamePatternService = new();

    public Guid Start(
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        TemplateKind kind,
        OutputMode mode,
        string filenamePattern,
        string outputFolderPath,
        ListRenderSettings listSettings)
    {
        var jobId = Guid.NewGuid();
        var status = new OutputJobStatus { Total = rows.Count };
        _jobs[jobId] = status;

        Task.Run(() => Execute(status, templatePath, fields, rows, kind, mode, filenamePattern, outputFolderPath, listSettings));

        return jobId;
    }

    public OutputJobStatus? GetStatus(Guid jobId) => _jobs.TryGetValue(jobId, out var status) ? status : null;

    private void Execute(
        OutputJobStatus status,
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        TemplateKind kind,
        OutputMode mode,
        string filenamePattern,
        string outputFolderPath,
        ListRenderSettings listSettings)
    {
        try
        {
            Directory.CreateDirectory(outputFolderPath);

            // ジョブ内の全ページ・全ファイルで同じ時刻を使うため、実行開始時に一度だけ取得してフォーマットする。
            string outputDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

            if (kind == TemplateKind.List)
                RunList(status, templatePath, fields, rows, outputFolderPath, listSettings, outputDateTime);
            else if (mode == OutputMode.Combined)
                RunCombined(status, templatePath, fields, rows, outputFolderPath, outputDateTime);
            else
                RunIndividual(status, templatePath, fields, rows, filenamePattern, outputFolderPath, outputDateTime);

            status.State = OutputJobState.Completed;
        }
        catch (Exception ex)
        {
            status.ErrorMessage = ex.Message;
            status.State = OutputJobState.Failed;
        }
    }

    private void RunCombined(
        OutputJobStatus status,
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string outputFolderPath,
        string outputDateTime)
    {
        // 結合出力はCSV1行=1ページになるため、ページ番号は行番号と同じ値、総ページ数はrows.Countそのもの。
        using var combined = new PdfDocument();
        for (int i = 0; i < rows.Count; i++)
        {
            _composer.AppendComposedPage(combined, templatePath, fields, rows[i], rowNumber: i + 1, pageNumber: i + 1, totalPageCount: rows.Count, outputDateTime: outputDateTime);
            status.Processed++;
        }
        combined.Save(Path.Combine(outputFolderPath, "output.pdf"));
    }

    private void RunIndividual(
        OutputJobStatus status,
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string filenamePattern,
        string outputFolderPath,
        string outputDateTime)
    {
        // 個別出力は1行=1ファイル(1ページ)なので、ページ番号・総ページ数は常に1。行番号はCSV内の行位置を保つ。
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            using var document = _composer.ComposeSinglePage(templatePath, fields, row, rowNumber: i + 1, pageNumber: 1, totalPageCount: 1, outputDateTime: outputDateTime);
            string rawName = _fileNamePatternService.Resolve(filenamePattern, row, rowNumber: i + 1, outputDateTime: outputDateTime);
            string finalName = _fileNamePatternService.Deduplicate(rawName, usedNames);
            document.Save(Path.Combine(outputFolderPath, finalName));
            status.Processed++;
        }
    }

    /// <summary>一覧表出力: CSV全行を1つの一覧表として連続ページに描画し、常に1つのPDF(output.pdf)にまとめる。</summary>
    private void RunList(
        OutputJobStatus status,
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string outputFolderPath,
        ListRenderSettings listSettings,
        string outputDateTime)
    {
        using var combined = new PdfDocument();
        _composer.ComposeListPages(combined, templatePath, fields, rows, listSettings, processed => status.Processed = processed, outputDateTime);
        combined.Save(Path.Combine(outputFolderPath, "output.pdf"));
    }
}
