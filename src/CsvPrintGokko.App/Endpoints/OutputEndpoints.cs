using CsvPrintGokko.Core.Csv;
using CsvPrintGokko.Core.Jobs;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Templates;
using Microsoft.Extensions.Caching.Memory;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>PDF出力ジョブの開始・進捗確認を行うエンドポイント群。</summary>
public static class OutputEndpoints
{
    public static void MapOutputEndpoints(this WebApplication app)
    {
        app.MapPost("/api/output/start", (OutputStartRequest request, TemplateStore templateStore, IMemoryCache cache, OutputJobRunner jobRunner) =>
        {
            if (!cache.TryGetValue(request.CsvSessionId, out CsvTable? table) || table is null)
                return Results.NotFound("CSVセッションが見つかりません。CSVを読み込み直してください。");

            string pdfPath;
            TemplateKind kind;
            try
            {
                var layout = templateStore.GetLayout(request.TemplateId);
                pdfPath = templateStore.GetPdfPath(request.TemplateId);
                kind = layout.Kind;
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound("テンプレートが見つかりません。");
            }

            if (string.IsNullOrWhiteSpace(request.OutputFolderPath))
                return Results.BadRequest("保存先フォルダを指定してください。");
            // ファイル名パターンは単票の個別出力(1行=1ファイル)のときのみ使う。結合/一覧表では常に固定のファイル名で1つのPDFを出力する。
            if (kind == TemplateKind.Single && request.Mode == OutputMode.Individual && string.IsNullOrWhiteSpace(request.FilenamePattern))
                return Results.BadRequest("ファイル名パターンを指定してください。");

            var jobId = jobRunner.Start(
                pdfPath, request.Fields, table.Rows, kind, request.Mode,
                request.FilenamePattern, request.OutputFolderPath, request.ListSettings);
            return Results.Ok(new { jobId });
        });

        app.MapGet("/api/output/{jobId:guid}/status", (Guid jobId, OutputJobRunner jobRunner) =>
        {
            var status = jobRunner.GetStatus(jobId);
            if (status is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                state = status.State.ToString().ToLowerInvariant(),
                processed = status.Processed,
                total = status.Total,
                errorMessage = status.ErrorMessage
            });
        });
    }
}

/// <summary>出力ジョブ開始リクエスト。</summary>
public sealed record OutputStartRequest
{
    public required Guid TemplateId { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
    public required Guid CsvSessionId { get; init; }
    /// <summary>TemplateKind.Singleのときのみ使用(結合/個別)。Listでは無視される。</summary>
    public OutputMode Mode { get; init; } = OutputMode.Combined;
    /// <summary>TemplateKind.Single かつ Mode=Individual のときのみ必須。</summary>
    public string FilenamePattern { get; init; } = "";
    public required string OutputFolderPath { get; init; }
    /// <summary>TemplateKind.Listのときの一覧表示設定。</summary>
    public ListRenderSettings ListSettings { get; init; } = new();
}
