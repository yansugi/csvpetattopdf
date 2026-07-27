using CsvPrintGokko.Core.Models;
using CsvPrintGokko.Core.Templates;
using CsvPrintGokko.Core.Tests.TestSupport;

namespace CsvPrintGokko.Core.Tests.Templates;

public sealed class TemplateStoreTests : IDisposable
{
    private readonly string _rootDirectory;
    private readonly TemplateStore _sut;

    public TemplateStoreTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"csvprintgokko-tests-{Guid.NewGuid():N}");
        _sut = new TemplateStore(_rootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    [Fact]
    public void CreateTemplate_保存したPDFとページサイズからlayoutを生成する()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);

        var layout = _sut.CreateTemplate("請求書テンプレート", pdfStream);

        Assert.Equal("請求書テンプレート", layout.TemplateName);
        Assert.True(layout.PageSize.WidthPt > 0);
        Assert.True(layout.PageSize.HeightPt > 0);
        Assert.Empty(layout.Fields);
        Assert.Equal(OutputMode.Combined, layout.OutputSettings.Mode);
    }

    [Fact]
    public void SaveLayout_JSON往復後もフィールド設定を保持する()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);
        var created = _sut.CreateTemplate("往復テスト", pdfStream);

        var withField = created with
        {
            Fields = new[]
            {
                new FieldDefinition
                {
                    Id = Guid.NewGuid(),
                    CsvColumn = "氏名",
                    X = 32.0,
                    Y = 58.5,
                    FontFamily = "Yu Gothic",
                    FontSizePt = 11,
                    Color = "#1F2A2E",
                    Align = TextAlign.Left,
                    Overflow = OverflowBehavior.Shrink,
                    MaxWidthPt = 120.0
                }
            }
        };
        _sut.SaveLayout(withField);

        var reloaded = _sut.GetLayout(created.TemplateId);

        Assert.Single(reloaded.Fields);
        var field = reloaded.Fields[0];
        Assert.Equal("氏名", field.CsvColumn);
        Assert.Equal(32.0, field.X);
        Assert.Equal(TextAlign.Left, field.Align);
        Assert.Equal(OverflowBehavior.Shrink, field.Overflow);
        Assert.Equal(120.0, field.MaxWidthPt);
    }

    [Fact]
    public void ListTemplates_更新日時の新しい順に返す()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();

        using (var s1 = File.OpenRead(pdfPath)) _sut.CreateTemplate("A", s1);
        Thread.Sleep(10); // UpdatedAtUtcの解像度差を確実に出すための小休止
        using (var s2 = File.OpenRead(pdfPath)) _sut.CreateTemplate("B", s2);

        var list = _sut.ListTemplates();

        Assert.Equal(2, list.Count);
        Assert.Equal("B", list[0].TemplateName);
        Assert.Equal("A", list[1].TemplateName);
    }
}
