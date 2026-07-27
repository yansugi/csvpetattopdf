using System.Collections.Concurrent;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Output;
using CsvPrintGokko.Core.Pdf;
using PdfSharp.Pdf;

namespace CsvPrintGokko.Core.Jobs;

/// <summary>
/// CSVの全行分のPDF出力(結合/個別)をバックグラウンドTaskで実行し、進捗をポーリングで参照できるようにする。
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
        OutputMode mode,
        string filenamePattern,
        string outputFolderPath)
    {
        var jobId = Guid.NewGuid();
        var status = new OutputJobStatus { Total = rows.Count };
        _jobs[jobId] = status;

        Task.Run(() => Execute(status, templatePath, fields, rows, mode, filenamePattern, outputFolderPath));

        return jobId;
    }

    public OutputJobStatus? GetStatus(Guid jobId) => _jobs.TryGetValue(jobId, out var status) ? status : null;

    private void Execute(
        OutputJobStatus status,
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        OutputMode mode,
        string filenamePattern,
        string outputFolderPath)
    {
        try
        {
            Directory.CreateDirectory(outputFolderPath);

            if (mode == OutputMode.Combined)
                RunCombined(status, templatePath, fields, rows, outputFolderPath);
            else
                RunIndividual(status, templatePath, fields, rows, filenamePattern, outputFolderPath);

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
        string outputFolderPath)
    {
        using var combined = new PdfDocument();
        for (int i = 0; i < rows.Count; i++)
        {
            _composer.AppendComposedPage(combined, templatePath, fields, rows[i], rowNumber: i + 1);
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
        string outputFolderPath)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            using var document = _composer.ComposeSinglePage(templatePath, fields, row, rowNumber: i + 1);
            string rawName = _fileNamePatternService.Resolve(filenamePattern, row);
            string finalName = _fileNamePatternService.Deduplicate(rawName, usedNames);
            document.Save(Path.Combine(outputFolderPath, finalName));
            status.Processed++;
        }
    }
}
