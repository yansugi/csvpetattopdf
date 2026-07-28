using System.Text.Json;
using Jint;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// FieldDefinition.JavaScriptFormula(例: "Number(row[\"単価\"]) * Number(row[\"数量\"])")を
/// Jint(.NET製の軽量JavaScriptエンジン)で評価する。
/// rowにはCSVの値(列名→文字列)、rowNumberには1始まりの行番号を渡す。
/// 無限ループ等で描画がフリーズしないよう、実行時間に短いタイムアウトを設ける。
/// </summary>
public static class JsFormulaEvaluator
{
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>スクリプトを評価する。成功時はresultに評価結果(数値かどうかと表示用文字列)を返す。PDF描画時に使う。</summary>
    public static bool TryEvaluate(string script, IReadOnlyDictionary<string, string> rowData, int rowNumber, out JsFormulaResult result)
    {
        var debugResult = Evaluate(script, rowData, rowNumber);
        result = new JsFormulaResult(debugResult.IsNumber, debugResult.NumberValue, debugResult.DisplayText);
        return debugResult.Success;
    }

    /// <summary>
    /// スクリプトを評価し、console.log等の出力内容とエラーメッセージも含めて返す。
    /// エディタの「実行してテスト」(デバッグ実行)から使う。
    /// </summary>
    public static JsFormulaDebugResult Evaluate(string script, IReadOnlyDictionary<string, string> rowData, int rowNumber)
    {
        var consoleLines = new List<string>();

        if (string.IsNullOrWhiteSpace(script))
            return new JsFormulaDebugResult(false, false, 0, string.Empty, consoleLines, "式が入力されていません。");

        try
        {
            var engine = new Engine(options => options
                .TimeoutInterval(ExecutionTimeout)
                .LimitRecursion(100));

            // CSVの列名(日本語含む)をJSのプロパティキーとしてそのまま安全に使えるよう、
            // JSONとして組み立ててからJS側でパースさせる(CLR型のインターロップに頼らない)。
            string rowJson = JsonSerializer.Serialize(rowData);
            engine.Execute($"var row = {rowJson};");
            engine.SetValue("rowNumber", rowNumber);
            SetupConsole(engine, consoleLines);

            var value = engine.Evaluate(script);
            bool isNumber = value.IsNumber();
            string displayText = isNumber
                ? string.Empty
                : (value.IsUndefined() || value.IsNull() ? string.Empty : value.ToString());

            return new JsFormulaDebugResult(true, isNumber, isNumber ? value.AsNumber() : 0, displayText, consoleLines, null);
        }
        catch (Exception ex)
        {
            return new JsFormulaDebugResult(false, false, 0, string.Empty, consoleLines, ex.Message);
        }
    }

    /// <summary>
    /// console.log/warn/errorをJS側に用意し、呼び出し内容をconsoleLinesへ蓄積する。
    /// 本番のPDF描画時にconsole.logが残っていてもエラーにならないよう、常に用意する。
    /// </summary>
    private static void SetupConsole(Engine engine, List<string> consoleLines)
    {
        engine.SetValue("__nativeConsoleLog", new Action<string>(line => consoleLines.Add(line)));
        engine.Execute(@"
            var console = {
                log: function() { __nativeConsoleLog(Array.prototype.slice.call(arguments).join(' ')); },
                warn: function() { __nativeConsoleLog('[warn] ' + Array.prototype.slice.call(arguments).join(' ')); },
                error: function() { __nativeConsoleLog('[error] ' + Array.prototype.slice.call(arguments).join(' ')); }
            };
        ");
    }
}

/// <summary>JsFormulaEvaluatorの評価結果。IsNumberがtrueの場合は数値表示形式(桁区切り等)を適用できる。</summary>
public readonly record struct JsFormulaResult(bool IsNumber, double NumberValue, string DisplayText);

/// <summary>デバッグ実行の結果。ConsoleLinesはconsole.log等の出力、ErrorMessageは失敗時の例外メッセージ。</summary>
public readonly record struct JsFormulaDebugResult(
    bool Success,
    bool IsNumber,
    double NumberValue,
    string DisplayText,
    IReadOnlyList<string> ConsoleLines,
    string? ErrorMessage);
