using CsvPrintGokko.Core.Csv;
using CsvPrintGokko.Core.Pdf;
using Microsoft.Extensions.Caching.Memory;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>配置エディタのJavaScript式エディタから、保存前の式をその場でテスト実行するためのエンドポイント。</summary>
public static class FormulaEndpoints
{
    public static void MapFormulaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/formula/test-js", (TestJsFormulaRequest request, IMemoryCache cache) =>
        {
            IReadOnlyDictionary<string, string> rowData = new Dictionary<string, string>();
            int rowNumber = 1;

            // CSVが読み込まれていればそのCSVの指定行データで、無ければ空データ(row未定義相当)でテストする。
            if (request.CsvSessionId is { } sessionId)
            {
                if (!cache.TryGetValue(sessionId, out CsvTable? table) || table is null)
                    return Results.NotFound("CSVセッションが見つかりません。CSVを読み込み直してください。");

                int rowIndex = request.RowIndex ?? 0;
                if (rowIndex < 0 || rowIndex >= table.Rows.Count)
                    return Results.BadRequest($"行番号が範囲外です(0〜{table.Rows.Count - 1})。");

                rowData = table.Rows[rowIndex];
                rowNumber = rowIndex + 1;
            }

            var result = JsFormulaEvaluator.Evaluate(request.Script, rowData, rowNumber);
            return Results.Ok(new
            {
                success = result.Success,
                isNumber = result.IsNumber,
                numberValue = result.NumberValue,
                displayText = result.DisplayText,
                consoleLines = result.ConsoleLines,
                errorMessage = result.ErrorMessage
            });
        });
    }
}

/// <summary>JavaScript式のテスト実行リクエスト。CsvSessionId未指定時は空データで評価する。</summary>
public sealed record TestJsFormulaRequest
{
    public required string Script { get; init; }
    public Guid? CsvSessionId { get; init; }
    public int? RowIndex { get; init; }
}
