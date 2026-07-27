using PdfSharp.Pdf;

namespace CsvPrintGokko.Core.Tests.TestSupport;

/// <summary>テストで使う最小限の白紙PDFファイルを一時フォルダに生成するヘルパー。</summary>
internal static class TestPdfFactory
{
    public static string CreateBlankSinglePagePdf()
    {
        var document = new PdfDocument();
        document.AddPage();

        string path = Path.Combine(Path.GetTempPath(), $"csvprintgokko-test-{Guid.NewGuid():N}.pdf");
        document.Save(path);
        return path;
    }
}
