using System.Text.Json;
using CsvPrintGokko.Core.Json;
using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Templates;
using PdfSharp.Pdf.IO;

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
            string kindValue = form["kind"].ToString();

            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest("テンプレート名(name)を指定してください。");
            if (file is null || file.Length == 0)
                return Results.BadRequest("PDFファイル(file)を指定してください。");

            var kind = string.Equals(kindValue, "list", StringComparison.OrdinalIgnoreCase) ? TemplateKind.List : TemplateKind.Single;

            await using var stream = file.OpenReadStream();
            var layout = store.CreateTemplate(name, stream, kind);
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

        // テンプレートPDF実体の差し替え。ページサイズが変わった場合、既存フィールドの座標は
        // そのままなのでズレる可能性がある(UI側で警告する想定)。
        group.MapPost("/{id:guid}/pdf", async (Guid id, HttpRequest request, TemplateStore store) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-dataでfile(PDF)を送信してください。");

            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest("PDFファイル(file)を指定してください。");

            try
            {
                await using var stream = file.OpenReadStream();
                var updated = store.ReplacePdf(id, stream);
                return Results.Ok(updated);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
            catch (Exception ex) when (ex is PdfReaderException or InvalidDataException)
            {
                return Results.BadRequest($"PDFファイルの読み込みに失敗しました: {ex.Message}");
            }
        });

        // 名前を付けて保存: 編集中のレイアウト(未保存の変更を含む)をPDFごと新しいテンプレートとして複製する。
        // 複製元のテンプレートには一切手を加えない。
        group.MapPost("/{id:guid}/save-as", async (Guid id, HttpRequest request, TemplateStore store) =>
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

            try
            {
                var saved = store.SaveAsNewTemplate(id, incoming);
                return Results.Created($"/api/templates/{saved.TemplateId}", saved);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
        });

        // テンプレートの削除(PDF・layout.jsonごと完全に削除。元に戻せない)。
        group.MapDelete("/{id:guid}", (Guid id, TemplateStore store) =>
        {
            try
            {
                store.DeleteTemplate(id);
                return Results.NoContent();
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // プロジェクト一式(レイアウト+テンプレートPDF+CSV)を1つのファイルにまとめてダウンロードする。
        // 別の環境へ持ち出し、下の/importで読み込むことでそのまま作業を引き継げるようにする。
        group.MapGet("/{id:guid}/export", (Guid id, TemplateStore store) =>
        {
            try
            {
                byte[] zipBytes = store.ExportProject(id);
                var layout = store.GetLayout(id);
                string fileName = SanitizeFileName(layout.TemplateName) + ".cpgproj";
                return Results.File(zipBytes, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // /exportで作成したプロジェクトファイルをmultipart/form-dataのfileとして受け取り、
        // 新規テンプレートとして登録する(テンプレートIDは常に新規採番)。
        group.MapPost("/import", async (HttpRequest request, TemplateStore store) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-dataでfile(プロジェクトファイル)を送信してください。");

            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest("プロジェクトファイル(file)を指定してください。");

            try
            {
                await using var stream = file.OpenReadStream();
                var layout = store.ImportProject(stream);
                return Results.Created($"/api/templates/{layout.TemplateId}", layout);
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest($"プロジェクトファイルの読み込みに失敗しました: {ex.Message}");
            }
        });
    }

    /// <summary>ダウンロードファイル名として使えるよう、Windowsのファイル名に使えない文字を除去する。</summary>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(sanitized) ? "project" : sanitized;
    }
}
