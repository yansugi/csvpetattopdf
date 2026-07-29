using CsvPrintGokko.Core.Output;

namespace CsvPrintGokko.Core.Tests.Output;

public sealed class FileNamePatternServiceTests
{
    private readonly FileNamePatternService _sut = new();

    [Fact]
    public void Resolve_トークンを実データに置換する()
    {
        var row = new Dictionary<string, string> { ["氏名"] = "山田太郎", ["発行日"] = "2026-07-24" };

        string result = _sut.Resolve("{氏名}_{発行日}.pdf", row);

        Assert.Equal("山田太郎_2026-07-24.pdf", result);
    }

    [Fact]
    public void Resolve_拡張子が無ければpdfを補完する()
    {
        var row = new Dictionary<string, string> { ["氏名"] = "山田太郎" };

        string result = _sut.Resolve("{氏名}", row);

        Assert.Equal("山田太郎.pdf", result);
    }

    [Fact]
    public void Resolve_ファイル名に使えない文字をアンダースコアに置換する()
    {
        var row = new Dictionary<string, string> { ["住所"] = "東京都/千代田区:1-1" };

        string result = _sut.Resolve("{住所}.pdf", row);

        Assert.DoesNotContain('/', result);
        Assert.DoesNotContain(':', result);
        Assert.Equal("東京都_千代田区_1-1.pdf", result);
    }

    [Fact]
    public void Resolve_対応する列が無いトークンは空文字に置換する()
    {
        var row = new Dictionary<string, string> { ["氏名"] = "山田太郎" };

        string result = _sut.Resolve("{住所}_{氏名}.pdf", row);

        Assert.Equal("_山田太郎.pdf", result);
    }

    [Fact]
    public void Resolve_行番号トークンを1始まりの連番に置換する()
    {
        var row = new Dictionary<string, string> { ["氏名"] = "山田太郎" };

        string result = _sut.Resolve("{行番号}_{氏名}.pdf", row, rowNumber: 5, outputDateTime: "");

        Assert.Equal("5_山田太郎.pdf", result);
    }

    [Fact]
    public void Resolve_出力時間トークンをサニタイズして置換する()
    {
        var row = new Dictionary<string, string> { ["氏名"] = "山田太郎" };

        string result = _sut.Resolve("{氏名}_{出力時間}.pdf", row, rowNumber: 1, outputDateTime: "2026/07/28 21:00:00");

        Assert.Equal("山田太郎_2026_07_28 21_00_00.pdf", result);
    }

    [Fact]
    public void Deduplicate_初回はそのままの名前を返す()
    {
        var used = new HashSet<string>();

        string result = _sut.Deduplicate("山田太郎.pdf", used);

        Assert.Equal("山田太郎.pdf", result);
    }

    [Fact]
    public void Deduplicate_重複時は連番を付与する()
    {
        var used = new HashSet<string>();
        _sut.Deduplicate("山田太郎.pdf", used);

        string second = _sut.Deduplicate("山田太郎.pdf", used);
        string third = _sut.Deduplicate("山田太郎.pdf", used);

        Assert.Equal("山田太郎 (2).pdf", second);
        Assert.Equal("山田太郎 (3).pdf", third);
    }
}
