using System.Globalization;
using CsvPrintGokko.Core.Csv;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Pdf;
using CsvPrintGokko.Core.Templates;
using Microsoft.Extensions.Caching.Memory;
using PdfSharp.Pdf;

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
                // プレビューは都度その場で生成するため、{出力時間}には現在時刻をそのまま使う。
                string outputDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
                using var document = Composer.ComposeSinglePage(pdfPath, request.Fields, table.Rows[request.RowIndex], rowNumber: request.RowIndex + 1, pageNumber: request.RowIndex + 1, totalPageCount: table.Rows.Count, outputDateTime: outputDateTime);
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

        // 一覧表テンプレート(TemplateKind.List)専用のプレビュー。行送りではなく、CSVの先頭から一部の行を使って
        // 実際に複数ページに渡る一覧表(見出し・ゼブラ縞・改ページ含む)がどう見えるかを確認できるようにする。
        // 編集中にCSVが大量にある場合でも重くなり過ぎないよう、プレビューに使う行数はPreviewMaxRowsで打ち切る。
        app.MapPost("/api/preview/render-list", (PreviewListRenderRequest request, TemplateStore templateStore, IMemoryCache cache) =>
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

            byte[] pdfBytes;
            try
            {
                string outputDateTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
                using var document = new PdfDocument();
                Composer.ComposeListPages(document, pdfPath, request.Fields, table.Rows.Take(PreviewMaxRows).ToList(), request.ListSettings, outputDateTime: outputDateTime);
                using var memoryStream = new MemoryStream();
                document.Save(memoryStream, closeStream: false);
                pdfBytes = memoryStream.ToArray();
            }
            catch (Exception ex) when (ex is FormatException or FileNotFoundException or InvalidOperationException or InvalidDataException)
            {
                return Results.BadRequest($"プレビューの生成に失敗しました: {ex.Message}");
            }

            return Results.File(pdfBytes, "application/pdf");
        });
    }

    private const int PreviewMaxRows = 200;
}

/// <summary>プレビュー合成リクエスト。fields未保存の編集中状態をそのまま渡す。</summary>
public sealed record PreviewRenderRequest
{
    public required Guid TemplateId { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
    public required Guid CsvSessionId { get; init; }
    public required int RowIndex { get; init; }
}

/// <summary>一覧表プレビューの合成リクエスト。行送りではなくCSVの先頭から一部の行を使う。</summary>
public sealed record PreviewListRenderRequest
{
    public required Guid TemplateId { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
    public required Guid CsvSessionId { get; init; }
    public ListRenderSettings ListSettings { get; init; } = new();
}
