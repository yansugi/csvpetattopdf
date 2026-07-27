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
}
