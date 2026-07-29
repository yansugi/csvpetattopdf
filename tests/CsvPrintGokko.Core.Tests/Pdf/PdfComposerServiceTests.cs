using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Pdf;
using CsvPrintGokko.Core.Tests.TestSupport;
using PdfSharp.Pdf;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class PdfComposerServiceTests
{
    private static FieldDefinition CreateNameField() => new()
    {
        Id = Guid.NewGuid(),
        CsvColumn = "氏名",
        X = 40,
        Y = 80,
        FontFamily = "Yu Gothic",
        FontSizePt = 12,
        Color = "#000000",
        Align = TextAlign.Left,
        Overflow = OverflowBehavior.None
    };

    [Fact]
    public void ComposeSinglePage_フィールドを描画した非空PDFを生成する()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var rowData = new Dictionary<string, string> { ["氏名"] = "山田太郎" };

        using var document = sut.ComposeSinglePage(templatePath, new[] { CreateNameField() }, rowData);
        // PDFsharpはSave後、in-memory表現(PageCount等)へのアクセスが不可になる仕様のため、
        // 保存前に検証しておく。
        Assert.Equal(1, document.PageCount);

        string outputPath = Path.Combine(Path.GetTempPath(), $"csvprintgokko-compose-{Guid.NewGuid():N}.pdf");
        document.Save(outputPath);

        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public void AppendComposedPage_結合出力用に複数ページを積み上げられる()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var field = CreateNameField();

        using var combined = new PdfDocument();
        sut.AppendComposedPage(combined, templatePath, new[] { field }, new Dictionary<string, string> { ["氏名"] = "山田太郎" });
        sut.AppendComposedPage(combined, templatePath, new[] { field }, new Dictionary<string, string> { ["氏名"] = "鈴木花子" });

        Assert.Equal(2, combined.PageCount);
    }

    [Fact]
    public void ComposeSinglePage_対応する列が無いフィールドは例外にせず無視する()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var rowData = new Dictionary<string, string> { ["住所"] = "東京都千代田区" }; // "氏名"列が存在しない

        using var document = sut.ComposeSinglePage(templatePath, new[] { CreateNameField() }, rowData);

        Assert.Equal(1, document.PageCount);
    }

    // Y=40は各テストのListRenderSettings.RowOriginY(=40)と合わせてあり、
    // 「繰り返し行の枠」内に収まる位置に配置することで自動的に繰り返し対象として扱われる。
    private static FieldDefinition CreateRepeatingField(string csvColumn, double x, DataType dataType = DataType.Text) => new()
    {
        Id = Guid.NewGuid(),
        CsvColumn = csvColumn,
        X = x,
        Y = 40,
        FontFamily = "Yu Gothic",
        FontSizePt = 10,
        Color = "#000000",
        Align = TextAlign.Left,
        MaxWidthPt = 80,
        DataType = dataType,
        NumberUseThousandsSeparator = dataType == DataType.Number
    };

    private static List<IReadOnlyDictionary<string, string>> CreateRows(int count) =>
        Enumerable.Range(1, count)
            .Select(i => (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["氏名"] = $"テスト{i}", ["金額"] = (i * 100).ToString() })
            .ToList();

    [Fact]
    public void ComposeListPages_1ページに収まる行数なら1ページで出力する()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var fields = new[] { CreateRepeatingField("氏名", 40), CreateRepeatingField("金額", 120, DataType.Number) };
        var settings = new ListRenderSettings { RowOriginY = 40, RowHeightPt = 20, RepeatCount = 4 };

        using var document = new PdfDocument();
        sut.ComposeListPages(document, templatePath, fields, CreateRows(4), settings);

        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void ComposeListPages_収まらない行数は複数ページに分割する()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var fields = new[] { CreateRepeatingField("氏名", 40), CreateRepeatingField("金額", 120, DataType.Number) };
        var settings = new ListRenderSettings { RowOriginY = 40, RowHeightPt = 20, RepeatCount = 2 }; // 1ページ2行

        using var document = new PdfDocument();
        sut.ComposeListPages(document, templatePath, fields, CreateRows(5), settings);

        Assert.Equal(3, document.PageCount); // 2+2+1行 = 3ページ
    }

    [Fact]
    public void ComposeListPages_進捗コールバックが累積した処理行数を通知する()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var fields = new[] { CreateRepeatingField("氏名", 40) };
        var settings = new ListRenderSettings { RowOriginY = 40, RowHeightPt = 20, RepeatCount = 2 }; // 1ページ2行
        var reported = new List<int>();

        using var document = new PdfDocument();
        sut.ComposeListPages(document, templatePath, fields, CreateRows(5), settings, reported.Add);

        Assert.Equal(new[] { 2, 4, 5 }, reported);
    }

    [Fact]
    public void ComposeListPages_枠外のフィールドは繰り返し行に含まれず固定描画される()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();
        var insideField = CreateRepeatingField("氏名", 40); // Y=40、枠(40〜60)内
        var outsideField = insideField with { Y = 400 }; // 枠の外→固定フィールド扱い
        var settings = new ListRenderSettings { RowOriginY = 40, RowHeightPt = 20, RepeatCount = 2 }; // 1ページ2行

        using var document = new PdfDocument();
        sut.ComposeListPages(document, templatePath, new[] { insideField, outsideField }, CreateRows(5), settings);

        Assert.Equal(3, document.PageCount); // 枠外フィールドはページ数計算(2+2+1行)に影響しない
    }

    [Fact]
    public void ComposeListPages_繰り返し行のフィールドが無い場合は例外を投げる()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        var sut = new PdfComposerService();

        using var document = new PdfDocument();
        Assert.Throws<InvalidOperationException>(() =>
            sut.ComposeListPages(document, templatePath, Array.Empty<FieldDefinition>(), CreateRows(1), new ListRenderSettings()));
    }
}
