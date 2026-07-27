using CsvPrintGokko.App.Services;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>ネイティブのフォルダ選択ダイアログ・エクスプローラー起動を扱うエンドポイント群。</summary>
public static class DialogEndpoints
{
    private static readonly TimeSpan BrowseFolderTimeout = TimeSpan.FromMinutes(5);

    public static void MapDialogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dialogs");

        group.MapPost("/browse-folder", async (StaFolderDialogService dialogService) =>
        {
            try
            {
                string? path = await dialogService.BrowseFolderAsync("出力先フォルダを選択してください", BrowseFolderTimeout);
                return Results.Ok(new { path });
            }
            catch (TimeoutException)
            {
                return Results.Problem("フォルダ選択がタイムアウトしました。もう一度お試しください。", statusCode: StatusCodes.Status504GatewayTimeout);
            }
        });

        group.MapPost("/open-folder", (OpenFolderRequest request) =>
        {
            if (!Directory.Exists(request.Path))
                return Results.BadRequest("指定されたフォルダが見つかりません。");

            System.Diagnostics.Process.Start("explorer.exe", request.Path);
            return Results.Ok();
        });

        group.MapPost("/browse-csv-file", async (StaFolderDialogService dialogService) =>
        {
            try
            {
                string? path = await dialogService.BrowseFileAsync(
                    "CSVファイルを選択してください",
                    "CSVファイル (*.csv)|*.csv|すべてのファイル (*.*)|*.*",
                    BrowseFolderTimeout);
                return Results.Ok(new { path });
            }
            catch (TimeoutException)
            {
                return Results.Problem("ファイル選択がタイムアウトしました。もう一度お試しください。", statusCode: StatusCodes.Status504GatewayTimeout);
            }
        });
    }
}

public sealed record OpenFolderRequest
{
    public required string Path { get; init; }
}
