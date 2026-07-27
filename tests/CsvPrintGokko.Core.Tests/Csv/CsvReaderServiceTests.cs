using System.Text;
using CsvPrintGokko.Core.Csv;
using CsvPrintGokko.Core.Models;

namespace CsvPrintGokko.Core.Tests.Csv;

public sealed class CsvReaderServiceTests
{
    private readonly CsvReaderService _sut = new();

    [Fact]
    public void Read_UTF8のCSVを正しく読み込む()
    {
        string content = "氏名,住所,金額\n山田太郎,東京都千代田区,12345\n鈴木花子,大阪府大阪市,67890\n";
        using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(content));
        var settings = new CsvSettings { Encoding = CsvEncoding.Utf8, Delimiter = ",", HasHeader = true };

        var table = _sut.Read(stream, settings);

        Assert.Equal(new[] { "氏名", "住所", "金額" }, table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("山田太郎", table.Rows[0]["氏名"]);
        Assert.Equal("大阪府大阪市", table.Rows[1]["住所"]);
    }

    [Fact]
    public void Read_ShiftJISのCSVを正しく読み込む()
    {
        string content = "氏名,住所,金額\n山田太郎,東京都千代田区,12345\n鈴木花子,大阪府大阪市,67890\n";
        var sjis = Encoding.GetEncoding("shift_jis");
        using var stream = new MemoryStream(sjis.GetBytes(content));
        var settings = new CsvSettings { Encoding = CsvEncoding.ShiftJis, Delimiter = ",", HasHeader = true };

        var table = _sut.Read(stream, settings);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("山田太郎", table.Rows[0]["氏名"]);
        Assert.Equal("67890", table.Rows[1]["金額"]);
    }

    [Fact]
    public void Read_タブ区切りを指定した場合は区切り文字として認識する()
    {
        string content = "氏名\t住所\n山田太郎\t東京都千代田区\n";
        using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(content));
        var settings = new CsvSettings { Encoding = CsvEncoding.Utf8, Delimiter = "\t", HasHeader = true };

        var table = _sut.Read(stream, settings);

        Assert.Equal(new[] { "氏名", "住所" }, table.Headers);
        Assert.Single(table.Rows);
        Assert.Equal("東京都千代田区", table.Rows[0]["住所"]);
    }

    [Fact]
    public void Read_ヘッダー無しの場合は列名を自動採番する()
    {
        string content = "山田太郎,東京都千代田区\n鈴木花子,大阪府大阪市\n";
        using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(content));
        var settings = new CsvSettings { Encoding = CsvEncoding.Utf8, Delimiter = ",", HasHeader = false };

        var table = _sut.Read(stream, settings);

        Assert.Equal(new[] { "列1", "列2" }, table.Headers);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("山田太郎", table.Rows[0]["列1"]);
    }
}
