using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Pdf;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class CsvValueFormatterTests
{
    private static FieldDefinition CreateField() => new()
    {
        Id = Guid.NewGuid(),
        CsvColumn = "col",
        X = 0,
        Y = 0,
        FontFamily = "Yu Gothic",
        FontSizePt = 12,
        Color = "#000000",
        Align = TextAlign.Left
    };

    [Fact]
    public void Format_DataTypeText_無変換でそのまま返す()
    {
        var field = CreateField() with { DataType = DataType.Text };
        Assert.Equal("hello", CsvValueFormatter.Format(field, "hello"));
    }

    [Theory]
    [InlineData(DateFormatKind.Slash, "2026/07/27")]
    [InlineData(DateFormatKind.Kanji, "2026年07月27日")]
    [InlineData(DateFormatKind.MonthDay, "07/27")]
    [InlineData(DateFormatKind.Japanese, "令和8年7月27日")]
    public void Format_DataTypeDate_各表示形式に変換する(DateFormatKind kind, string expected)
    {
        var field = CreateField() with { DataType = DataType.Date, DateFormatKind = kind };
        Assert.Equal(expected, CsvValueFormatter.Format(field, "2026-07-27"));
    }

    [Theory]
    [InlineData(DateFormatKind.SlashWithTime, "2026/07/27 14:30")]
    [InlineData(DateFormatKind.KanjiWithTime, "2026年07月27日 14時30分")]
    [InlineData(DateFormatKind.TimeOnly, "14:30")]
    [InlineData(DateFormatKind.JapaneseWithTime, "令和8年7月27日 14時30分")]
    public void Format_DataTypeDate_時刻を含む表示形式に変換する(DateFormatKind kind, string expected)
    {
        var field = CreateField() with { DataType = DataType.Date, DateFormatKind = kind };
        Assert.Equal(expected, CsvValueFormatter.Format(field, "2026-07-27 14:30:00"));
    }

    [Fact]
    public void Format_DataTypeDate_カスタム書式を使える()
    {
        var field = CreateField() with { DataType = DataType.Date, DateFormatKind = DateFormatKind.Custom, DateCustomFormat = "yyyy.MM.dd" };
        Assert.Equal("2026.07.27", CsvValueFormatter.Format(field, "2026-07-27"));
    }

    [Fact]
    public void Format_DataTypeDate_解析できない値は元の文字列のまま返す()
    {
        var field = CreateField() with { DataType = DataType.Date };
        Assert.Equal("該当なし", CsvValueFormatter.Format(field, "該当なし"));
    }

    [Fact]
    public void Format_DataTypeNumber_桁区切りと小数桁数を適用する()
    {
        var field = CreateField() with { DataType = DataType.Number, NumberUseThousandsSeparator = true, NumberDecimalPlaces = 2 };
        Assert.Equal("1,234.50", CsvValueFormatter.Format(field, "1234.5"));
    }

    [Fact]
    public void Format_DataTypeNumber_接頭辞接尾辞を付与する()
    {
        var field = CreateField() with { DataType = DataType.Number, NumberPrefix = "¥", NumberSuffix = "円", NumberDecimalPlaces = 0, NumberUseThousandsSeparator = true };
        Assert.Equal("¥1,000円", CsvValueFormatter.Format(field, "1000"));
    }

    [Fact]
    public void Format_DataTypeNumber_解析できない値は元の文字列のまま返す()
    {
        var field = CreateField() with { DataType = DataType.Number };
        Assert.Equal("N/A", CsvValueFormatter.Format(field, "N/A"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("○")]
    [InlineData("有")]
    [InlineData("済")]
    [InlineData(" TRUE ")]
    public void Format_DataTypeBoolean_既定の真値リストに一致すれば真の表示になる(string rawValue)
    {
        var field = CreateField() with { DataType = DataType.Boolean };
        Assert.Equal("✓", CsvValueFormatter.Format(field, rawValue));
    }

    [Fact]
    public void Format_DataTypeBoolean_真値リストに一致しなければ偽の表示になる()
    {
        var field = CreateField() with { DataType = DataType.Boolean };
        Assert.Equal("", CsvValueFormatter.Format(field, "false"));
    }

    [Fact]
    public void Format_DataTypeBoolean_真偽の表示文字列をカスタマイズできる()
    {
        var field = CreateField() with { DataType = DataType.Boolean, BooleanTrueDisplay = "○", BooleanFalseDisplay = "×" };
        Assert.Equal("○", CsvValueFormatter.Format(field, "1"));
        Assert.Equal("×", CsvValueFormatter.Format(field, "0"));
    }
}
