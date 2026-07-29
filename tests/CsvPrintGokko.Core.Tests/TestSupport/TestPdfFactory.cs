using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace CsvPrintGokko.Core.Tests.TestSupport;

/// <summary>テストで使う最小限の白紙PDFファイルを一時フォルダに生成するヘルパー。</summary>
internal static class TestPdfFactory
{
    public static string CreateBlankSinglePagePdf(double? widthPt = null, double? heightPt = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        if (widthPt.HasValue && heightPt.HasValue)
        {
            page.Width = XUnit.FromPoint(widthPt.Value);
            page.Height = XUnit.FromPoint(heightPt.Value);
        }

        string path = Path.Combine(Path.GetTempPath(), $"csvprintgokko-test-{Guid.NewGuid():N}.pdf");
        document.Save(path);
        return path;
    }
}
