using System.Text.Json;
using CsvPrintGokko.Core.Json;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Templates;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>テンプレート(PDF+レイアウト設定)の作成・取得・一覧・更新を行うエンドポイント群。</summary>
public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/templates");

        // 新規テンプレート作成: multipart/form-dataでname(テンプレート名)とfile(PDF)を受け取る。
        group.MapPost("/", async (HttpRequest request, TemplateStore store) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-dataでname(テンプレート名)とfile(PDF)を送信してください。");

            var form = await request.ReadFormAsync();
            string? name = form["name"];
            var file = form.Files["file"];

            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest("テンプレート名(name)を指定してください。");
            if (file is null || file.Length == 0)
                return Results.BadRequest("PDFファイル(file)を指定してください。");

            await using var stream = file.OpenReadStream();
            var layout = store.CreateTemplate(name, stream);
            return Results.Created($"/api/templates/{layout.TemplateId}", layout);
        });

        // テンプレート一覧(ホーム画面用)。
        group.MapGet("/", (TemplateStore store) => Results.Ok(store.ListTemplates()));

        // 単一テンプレートのレイアウト取得(配置エディタを開く際に使用)。
        group.MapGet("/{id:guid}", (Guid id, TemplateStore store) =>
        {
            try
            {
                return Results.Ok(store.GetLayout(id));
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // レイアウトの保存。
        group.MapPut("/{id:guid}/layout", async (Guid id, HttpRequest request, TemplateStore store) =>
        {
            TemplateLayout? incoming;
            try
            {
                incoming = await JsonSerializer.DeserializeAsync<TemplateLayout>(request.Body, JsonDefaults.Options);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest($"レイアウトJSONの形式が不正です: {ex.Message}");
            }

            if (incoming is null)
                return Results.BadRequest("レイアウトJSONを指定してください。");
            if (incoming.TemplateId != id)
                return Results.BadRequest("URLのテンプレートIDと本文のtemplateIdが一致しません。");

            var saved = store.SaveLayout(incoming);
            return Results.Ok(saved);
        });

        // テンプレートPDF実体のストリーム配信(pdf.jsでの表示に使用)。
        group.MapGet("/{id:guid}/pdf", (Guid id, TemplateStore store) =>
        {
            try
            {
                string path = store.GetPdfPath(id);
                return Results.File(path, "application/pdf");
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });
    }
}
