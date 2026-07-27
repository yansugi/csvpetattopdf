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
            try
            {
                pdfPath = templateStore.GetPdfPath(request.TemplateId);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound("テンプレートが見つかりません。");
            }

            if (string.IsNullOrWhiteSpace(request.OutputFolderPath))
                return Results.BadRequest("保存先フォルダを指定してください。");
            if (string.IsNullOrWhiteSpace(request.FilenamePattern))
                return Results.BadRequest("ファイル名パターンを指定してください。");

            var jobId = jobRunner.Start(pdfPath, request.Fields, table.Rows, request.Mode, request.FilenamePattern, request.OutputFolderPath);
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
    public required OutputMode Mode { get; init; }
    public required string FilenamePattern { get; init; }
    public required string OutputFolderPath { get; init; }
}
