using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Pdf;
using CsvPrintGokko.Core.Tests.TestSupport;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class QrCodeRendererTests
{
    private static FieldDefinition CreateQrField(double? sizePt = 80, string? backgroundColor = "#FFFFFF") => new()
    {
        Id = Guid.NewGuid(),
        Kind = FieldKind.Qr,
        X = 10,
        Y = 10,
        MaxWidthPt = sizePt,
        BackgroundColor = backgroundColor,
        FontFamily = "Yu Gothic",
        FontSizePt = 12,
        Color = "#000000",
        Align = TextAlign.Left
    };

    [Fact]
    public void TryDraw_通常の文字列はtrueを返しエラーメッセージを出さない()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        using var gfx = XGraphics.FromPdfPage(document.Pages[0]);

        bool result = QrCodeRenderer.TryDraw(gfx, CreateQrField(), "https://example.com", out string? error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void TryDraw_サイズが0以下の場合はfalseを返す()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        using var gfx = XGraphics.FromPdfPage(document.Pages[0]);

        bool result = QrCodeRenderer.TryDraw(gfx, CreateQrField(sizePt: 0), "https://example.com", out string? error);

        Assert.False(result);
        Assert.Equal("#ERROR", error);
    }

    [Fact]
    public void TryDraw_QRコード仕様の上限を超える長大な内容はfalseを返す()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        using var gfx = XGraphics.FromPdfPage(document.Pages[0]);

        // QRコード(バージョン40・誤り訂正M)のバイトモードでの最大収容文字数(約2953バイト)を超える文字列。
        string tooLong = new string('a', 5000);

        bool result = QrCodeRenderer.TryDraw(gfx, CreateQrField(), tooLong, out string? error);

        Assert.False(result);
        Assert.Equal("#ERROR", error);
    }

    [Fact]
    public void TryDraw_背景色未指定でも例外にならず描画できる()
    {
        string templatePath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        using var gfx = XGraphics.FromPdfPage(document.Pages[0]);

        bool result = QrCodeRenderer.TryDraw(gfx, CreateQrField(backgroundColor: null), "テスト", out string? error);

        Assert.True(result);
        Assert.Null(error);
    }
}
