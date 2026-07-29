using CsvPrintGokko.Core.Pdf;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class FormulaEvaluatorTests
{
    [Fact]
    public void TryEvaluate_列同士の掛け算を計算する()
    {
        var rowData = new Dictionary<string, string> { ["単価"] = "120", ["数量"] = "3" };
        Assert.True(FormulaEvaluator.TryEvaluate("{単価}*{数量}", rowData, 1, 1, 1, out double result));
        Assert.Equal(360, result);
    }

    [Fact]
    public void TryEvaluate_四則演算と括弧の優先順位を正しく処理する()
    {
        var rowData = new Dictionary<string, string> { ["A"] = "10", ["B"] = "2", ["C"] = "3" };
        Assert.True(FormulaEvaluator.TryEvaluate("({A}+{B})*{C}", rowData, 1, 1, 1, out double result));
        Assert.Equal(36, result);
    }

    [Fact]
    public void TryEvaluate_行番号トークンを1始まりの連番として使える()
    {
        var rowData = new Dictionary<string, string>();
        Assert.True(FormulaEvaluator.TryEvaluate("{行番号}*100", rowData, 5, 1, 1, out double result));
        Assert.Equal(500, result);
    }

    [Fact]
    public void TryEvaluate_ページ番号トークンを参照できる()
    {
        var rowData = new Dictionary<string, string>();
        Assert.True(FormulaEvaluator.TryEvaluate("{ページ番号}*100", rowData, 1, 3, 7, out double result));
        Assert.Equal(300, result);
    }

    [Fact]
    public void TryEvaluate_総ページ数トークンを参照できる()
    {
        var rowData = new Dictionary<string, string>();
        Assert.True(FormulaEvaluator.TryEvaluate("{総ページ数}*10", rowData, 1, 1, 7, out double result));
        Assert.Equal(70, result);
    }

    [Fact]
    public void TryEvaluate_変数を含まない数式もそのまま計算できる()
    {
        var rowData = new Dictionary<string, string>();
        Assert.True(FormulaEvaluator.TryEvaluate("1 + 2 * 3", rowData, 1, 1, 1, out double result));
        Assert.Equal(7, result);
    }

    [Fact]
    public void TryEvaluate_存在しない列を参照した場合は失敗する()
    {
        var rowData = new Dictionary<string, string> { ["単価"] = "100" };
        Assert.False(FormulaEvaluator.TryEvaluate("{単価}*{存在しない列}", rowData, 1, 1, 1, out _));
    }

    [Fact]
    public void TryEvaluate_列の値が数値でない場合は失敗する()
    {
        var rowData = new Dictionary<string, string> { ["氏名"] = "山田太郎" };
        Assert.False(FormulaEvaluator.TryEvaluate("{氏名}+1", rowData, 1, 1, 1, out _));
    }

    [Fact]
    public void TryEvaluate_0除算は失敗する()
    {
        var rowData = new Dictionary<string, string> { ["A"] = "10", ["B"] = "0" };
        Assert.False(FormulaEvaluator.TryEvaluate("{A}/{B}", rowData, 1, 1, 1, out _));
    }

    [Fact]
    public void TryEvaluate_構文エラーの数式は失敗する()
    {
        var rowData = new Dictionary<string, string> { ["A"] = "10" };
        Assert.False(FormulaEvaluator.TryEvaluate("{A}+*2", rowData, 1, 1, 1, out _));
    }

    [Fact]
    public void TryEvaluate_閉じ括弧が無い数式は失敗する()
    {
        var rowData = new Dictionary<string, string> { ["A"] = "10" };
        Assert.False(FormulaEvaluator.TryEvaluate("({A}+1", rowData, 1, 1, 1, out _));
    }
}
