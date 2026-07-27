using CsvPrintGokko.Core.Pdf;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class TextVariableResolverTests
{
    [Fact]
    public void Resolve_テキストの途中の変数を実データに置換する()
    {
        var rowData = new Dictionary<string, string> { ["氏名"] = "山田太郎" };
        Assert.Equal("こんにちは、山田太郎様", TextVariableResolver.Resolve("こんにちは、{氏名}様", rowData));
    }

    [Fact]
    public void Resolve_複数の変数を置換する()
    {
        var rowData = new Dictionary<string, string> { ["氏名"] = "山田太郎", ["日付"] = "2026/07/27" };
        Assert.Equal("山田太郎様(2026/07/27)", TextVariableResolver.Resolve("{氏名}様({日付})", rowData));
    }

    [Fact]
    public void Resolve_対応する列が無い変数は空文字になる()
    {
        var rowData = new Dictionary<string, string> { ["氏名"] = "山田太郎" };
        Assert.Equal("様", TextVariableResolver.Resolve("{存在しない列}様", rowData));
    }

    [Fact]
    public void Resolve_変数が無いテキストはそのまま返す()
    {
        var rowData = new Dictionary<string, string>();
        Assert.Equal("固定の案内文", TextVariableResolver.Resolve("固定の案内文", rowData));
    }
}
