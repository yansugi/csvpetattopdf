using CsvPrintGokko.Core.Pdf;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class JsFormulaEvaluatorTests
{
    [Fact]
    public void TryEvaluate_row変数でCSVの値を参照して計算できる()
    {
        var rowData = new Dictionary<string, string> { ["単価"] = "120", ["数量"] = "3" };
        Assert.True(JsFormulaEvaluator.TryEvaluate(
            "Number(row[\"単価\"]) * Number(row[\"数量\"])", rowData, 1, out var result));
        Assert.True(result.IsNumber);
        Assert.Equal(360, result.NumberValue);
    }

    [Fact]
    public void TryEvaluate_rowNumber変数で行番号を参照できる()
    {
        var rowData = new Dictionary<string, string>();
        Assert.True(JsFormulaEvaluator.TryEvaluate("rowNumber * 10", rowData, 5, out var result));
        Assert.True(result.IsNumber);
        Assert.Equal(50, result.NumberValue);
    }

    [Fact]
    public void TryEvaluate_条件分岐で文字列を返せる()
    {
        var rowData = new Dictionary<string, string> { ["性別"] = "女" };
        Assert.True(JsFormulaEvaluator.TryEvaluate(
            "row[\"性別\"] === \"男\" ? \"様\" : \"さん\"", rowData, 1, out var result));
        Assert.False(result.IsNumber);
        Assert.Equal("さん", result.DisplayText);
    }

    [Fact]
    public void TryEvaluate_日本語の列名をそのままプロパティとして参照できる()
    {
        var rowData = new Dictionary<string, string> { ["氏名"] = "山田太郎" };
        Assert.True(JsFormulaEvaluator.TryEvaluate("row[\"氏名\"] + \"様\"", rowData, 1, out var result));
        Assert.Equal("山田太郎様", result.DisplayText);
    }

    [Fact]
    public void TryEvaluate_構文エラーは失敗する()
    {
        var rowData = new Dictionary<string, string>();
        Assert.False(JsFormulaEvaluator.TryEvaluate("1 +* 2", rowData, 1, out _));
    }

    [Fact]
    public void TryEvaluate_未定義の変数を参照すると失敗する()
    {
        var rowData = new Dictionary<string, string>();
        Assert.False(JsFormulaEvaluator.TryEvaluate("undefinedVariable + 1", rowData, 1, out _));
    }

    [Fact]
    public void TryEvaluate_無限ループはタイムアウトして失敗する()
    {
        var rowData = new Dictionary<string, string>();
        Assert.False(JsFormulaEvaluator.TryEvaluate("while(true) {}", rowData, 1, out _));
    }

    [Fact]
    public void TryEvaluate_空文字は失敗する()
    {
        var rowData = new Dictionary<string, string>();
        Assert.False(JsFormulaEvaluator.TryEvaluate("", rowData, 1, out _));
    }

    [Fact]
    public void Evaluate_console_logの内容をConsoleLinesに蓄積する()
    {
        var rowData = new Dictionary<string, string> { ["単価"] = "100" };
        var result = JsFormulaEvaluator.Evaluate("console.log('単価は', row[\"単価\"]); Number(row[\"単価\"]) * 2", rowData, 1);

        Assert.True(result.Success);
        Assert.Equal(200, result.NumberValue);
        Assert.Single(result.ConsoleLines);
        Assert.Equal("単価は 100", result.ConsoleLines[0]);
    }

    [Fact]
    public void Evaluate_複数回のconsole_log呼び出しを順番に蓄積する()
    {
        var rowData = new Dictionary<string, string>();
        var result = JsFormulaEvaluator.Evaluate("console.log('1回目'); console.log('2回目'); 42", rowData, 1);

        Assert.True(result.Success);
        Assert.Equal(new[] { "1回目", "2回目" }, result.ConsoleLines);
    }

    [Fact]
    public void Evaluate_失敗時はErrorMessageが設定される()
    {
        var rowData = new Dictionary<string, string>();
        var result = JsFormulaEvaluator.Evaluate("undefinedVariable + 1", rowData, 1);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public void Evaluate_失敗前に出力されたconsole_logは失われない()
    {
        var rowData = new Dictionary<string, string>();
        var result = JsFormulaEvaluator.Evaluate("console.log('ここまでは実行される'); undefinedVariable + 1", rowData, 1);

        Assert.False(result.Success);
        Assert.Equal(new[] { "ここまでは実行される" }, result.ConsoleLines);
    }

    [Fact]
    public void TryEvaluate_本番描画時にconsole_logが残っていてもエラーにならない()
    {
        var rowData = new Dictionary<string, string> { ["単価"] = "100" };
        Assert.True(JsFormulaEvaluator.TryEvaluate("console.log('debug'); Number(row[\"単価\"])", rowData, 1, out var result));
        Assert.Equal(100, result.NumberValue);
    }
}
