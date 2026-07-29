using System.Text;
using CsvHelper;
using CsvPrintGokko.Core.Csv;
using CsvPrintGokko.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>CSVアップロード・パース結果のキャッシュ・行取得を行うエンドポイント群。</summary>
public static class CsvEndpoints
{
    /// <summary>放置されたCSVセッションがメモリを圧迫し続けないようにする有効期限。</summary>
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(4);

    public static void MapCsvEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/csv");

        // CSVアップロード: multipart/form-dataでfile/encoding/delimiter/hasHeaderを受け取り、
        // パース結果をIMemoryCacheにcsvSessionIdで保持する。
        group.MapPost("/load", async (HttpRequest request, IMemoryCache cache) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("multipart/form-dataでfile(CSV)とencoding/delimiter/hasHeaderを送信してください。");

            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest("CSVファイル(file)を指定してください。");

            string encodingValue = form["encoding"].ToString();
            string delimiter = form["delimiter"].ToString();
            if (string.IsNullOrEmpty(delimiter))
                delimiter = ",";
            if (!bool.TryParse(form["hasHeader"].ToString(), out bool hasHeader))
                hasHeader = true;

            CsvEncoding encoding;
            try
            {
                encoding = CsvEncodingExtensions.ParseCsvEncoding(string.IsNullOrEmpty(encodingValue) ? "utf-8" : encodingValue);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }

            var settings = new CsvSettings { Encoding = encoding, Delimiter = delimiter, HasHeader = hasHeader };

            CsvTable table;
            try
            {
                await using var stream = file.OpenReadStream();
                table = new CsvReaderService().Read(stream, settings);
            }
            catch (Exception ex) when (ex is CsvHelperException or InvalidDataException or DecoderFallbackException)
            {
                return Results.BadRequest($"CSVの読み込みに失敗しました。区切り文字やエンコーディングの指定を確認してください。({ex.Message})");
            }

            var sessionId = Guid.NewGuid();
            cache.Set(sessionId, table, SessionLifetime);

            return Results.Ok(new
            {
                csvSessionId = sessionId,
                headers = table.Headers,
                rowCount = table.Rows.Count
            });
        });

        // サーバー側の絶対パスから直接CSVを読み込む(プロジェクトを開き直した際の自動再読込用)。
        // ブラウザの<input type="file">は実ファイルパスを渡さないため、ネイティブダイアログ(browse-csv-file)で
        // 取得したパスをここに渡す想定。
        group.MapPost("/load-from-path", async (LoadCsvFromPathRequest request, IMemoryCache cache) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path) || !File.Exists(request.Path))
                return Results.NotFound("指定されたCSVファイルが見つかりません。");

            string delimiter = string.IsNullOrEmpty(request.Delimiter) ? "," : request.Delimiter;

            CsvEncoding encoding;
            try
            {
                encoding = CsvEncodingExtensions.ParseCsvEncoding(string.IsNullOrEmpty(request.Encoding) ? "utf-8" : request.Encoding);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }

            var settings = new CsvSettings { Encoding = encoding, Delimiter = delimiter, HasHeader = request.HasHeader };

            CsvTable table;
            try
            {
                await using var stream = File.OpenRead(request.Path);
                table = new CsvReaderService().Read(stream, settings);
            }
            catch (Exception ex) when (ex is CsvHelperException or InvalidDataException or DecoderFallbackException or IOException)
            {
                return Results.BadRequest($"CSVの読み込みに失敗しました。区切り文字やエンコーディングの指定を確認してください。({ex.Message})");
            }

            var sessionId = Guid.NewGuid();
            cache.Set(sessionId, table, SessionLifetime);

            return Results.Ok(new
            {
                csvSessionId = sessionId,
                headers = table.Headers,
                rowCount = table.Rows.Count
            });
        });

        // 行送りプレビュー用: 指定行のCSVデータ(列名→値)を返す。
        group.MapGet("/{sessionId:guid}/row/{index:int}", (Guid sessionId, int index, IMemoryCache cache) =>
        {
            if (!cache.TryGetValue(sessionId, out CsvTable? table) || table is null)
                return Results.NotFound("CSVセッションが見つかりません。CSVを読み込み直してください。");
            if (index < 0 || index >= table.Rows.Count)
                return Results.BadRequest($"行番号が範囲外です(0〜{table.Rows.Count - 1})。");

            return Results.Ok(table.Rows[index]);
        });

        // 出力設定画面のファイル名パターン変数ボタン用: CSVの列名一覧だけを取得する。
        // 配置エディタからsessionStorage経由で渡す方式だと、タブの再読込タイミング次第で
        // 情報が古いまま(または欠落したまま)になり得るため、csvSessionIdから都度サーバーへ問い合わせて
        // 常に最新の列名一覧を取得できるようにする。
        group.MapGet("/{sessionId:guid}/headers", (Guid sessionId, IMemoryCache cache) =>
        {
            if (!cache.TryGetValue(sessionId, out CsvTable? table) || table is null)
                return Results.NotFound("CSVセッションが見つかりません。CSVを読み込み直してください。");

            return Results.Ok(new { headers = table.Headers });
        });
    }
}

public sealed record LoadCsvFromPathRequest
{
    public required string Path { get; init; }
    public required string Encoding { get; init; }
    public required string Delimiter { get; init; }
    public required bool HasHeader { get; init; }
}
