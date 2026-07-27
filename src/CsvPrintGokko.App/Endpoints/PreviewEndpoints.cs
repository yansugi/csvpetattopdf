using CsvPrintGokko.Core.Csv;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Pdf;
using CsvPrintGokko.Core.Templates;
using Microsoft.Extensions.Caching.Memory;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>配置エディタでの行送りプレビュー用に、未保存のfields状態でPDFを合成するエンドポイント。</summary>
public static class PreviewEndpoints
{
    private static readonly PdfComposerService Composer = new();

    public static void MapPreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/preview/render", (PreviewRenderRequest request, TemplateStore templateStore, IMemoryCache cache) =>
        {
            if (!cache.TryGetValue(request.CsvSessionId, out CsvTable? table) || table is null)
                return Results.NotFound("CSVセッションが見つかりません。CSVを読み込み直してください。");
            if (request.RowIndex < 0 || request.RowIndex >= table.Rows.Count)
                return Results.BadRequest($"行番号が範囲外です(0〜{table.Rows.Count - 1})。");

            string pdfPath;
            try
            {
                pdfPath = templateStore.GetPdfPath(request.TemplateId);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound("テンプレートが見つかりません。");
            }

            byte[] pdfBytes;
            try
            {
                using var document = Composer.ComposeSinglePage(pdfPath, request.Fields, table.Rows[request.RowIndex], rowNumber: request.RowIndex + 1);
                using var memoryStream = new MemoryStream();
                document.Save(memoryStream, closeStream: false);
                pdfBytes = memoryStream.ToArray();
            }
            catch (Exception ex) when (ex is FormatException or FileNotFoundException)
            {
                return Results.BadRequest($"プレビューの生成に失敗しました: {ex.Message}");
            }

            return Results.File(pdfBytes, "application/pdf");
        });
    }
}

/// <summary>プレビュー合成リクエスト。fields未保存の編集中状態をそのまま渡す。</summary>
public sealed record PreviewRenderRequest
{
    public required Guid TemplateId { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
    public required Guid CsvSessionId { get; init; }
    public required int RowIndex { get; init; }
}
