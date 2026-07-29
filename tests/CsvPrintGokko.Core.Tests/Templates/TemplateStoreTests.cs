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

    [Fact]
    public void DeleteTemplate_削除後は一覧にも取得にも出てこなくなる()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);
        var created = _sut.CreateTemplate("削除対象", pdfStream);

        _sut.DeleteTemplate(created.TemplateId);

        Assert.Throws<FileNotFoundException>(() => _sut.GetLayout(created.TemplateId));
        Assert.Empty(_sut.ListTemplates());
    }

    [Fact]
    public void DeleteTemplate_存在しないIDを指定すると例外を投げる()
    {
        Assert.Throws<FileNotFoundException>(() => _sut.DeleteTemplate(Guid.NewGuid()));
    }

    [Fact]
    public void ExportProject_ImportProject_レイアウトPDF_CSVを一式別環境へ引き継げる()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        string csvPath = Path.Combine(Path.GetTempPath(), $"csvprintgokko-tests-{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "氏名,金額\n山田太郎,100\n");
        try
        {
            using var pdfStream = File.OpenRead(pdfPath);
            var created = _sut.CreateTemplate("引継ぎテスト", pdfStream);
            var withCsvAndField = created with
            {
                CsvSettings = created.CsvSettings with { LastFilePath = csvPath },
                Fields = new[]
                {
                    new FieldDefinition
                    {
                        Id = Guid.NewGuid(),
                        CsvColumn = "氏名",
                        X = 10, Y = 20,
                        FontFamily = "Yu Gothic", FontSizePt = 11, Color = "#000000",
                        Align = TextAlign.Left
                    }
                }
            };
            _sut.SaveLayout(withCsvAndField);

            byte[] zipBytes = _sut.ExportProject(created.TemplateId);

            // インポート先は別環境を模して、ルートディレクトリの異なる別のTemplateStoreインスタンスに読み込む。
            string otherRoot = Path.Combine(Path.GetTempPath(), $"csvprintgokko-tests-{Guid.NewGuid():N}");
            var otherStore = new TemplateStore(otherRoot);
            try
            {
                using var zipStream = new MemoryStream(zipBytes);
                var imported = otherStore.ImportProject(zipStream);

                Assert.NotEqual(created.TemplateId, imported.TemplateId);
                Assert.Equal("引継ぎテスト", imported.TemplateName);
                Assert.Single(imported.Fields);
                Assert.Equal("氏名", imported.Fields[0].CsvColumn);
                Assert.True(File.Exists(otherStore.GetPdfPath(imported.TemplateId)));
                Assert.NotNull(imported.CsvSettings.LastFilePath);
                Assert.True(File.Exists(imported.CsvSettings.LastFilePath));
                Assert.Equal("氏名,金額\n山田太郎,100\n", File.ReadAllText(imported.CsvSettings.LastFilePath!));
            }
            finally
            {
                if (Directory.Exists(otherRoot))
                    Directory.Delete(otherRoot, recursive: true);
            }
        }
        finally
        {
            if (File.Exists(csvPath))
                File.Delete(csvPath);
        }
    }

    [Fact]
    public void ExportProject_CSVファイルが見つからない場合はCSV無しで書き出しLastFilePathがnullになる()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);
        var created = _sut.CreateTemplate("CSV無しテスト", pdfStream);
        var withMissingCsv = created with
        {
            CsvSettings = created.CsvSettings with { LastFilePath = Path.Combine(Path.GetTempPath(), $"存在しない-{Guid.NewGuid():N}.csv") }
        };
        _sut.SaveLayout(withMissingCsv);

        byte[] zipBytes = _sut.ExportProject(created.TemplateId);

        string otherRoot = Path.Combine(Path.GetTempPath(), $"csvprintgokko-tests-{Guid.NewGuid():N}");
        var otherStore = new TemplateStore(otherRoot);
        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            var imported = otherStore.ImportProject(zipStream);

            Assert.Null(imported.CsvSettings.LastFilePath);
        }
        finally
        {
            if (Directory.Exists(otherRoot))
                Directory.Delete(otherRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveAsNewTemplate_編集中の内容をPDFごと新しいテンプレートとして複製し複製元は変更しない()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);
        var created = _sut.CreateTemplate("複製元", pdfStream);

        var editedLayout = created with
        {
            TemplateName = "複製先(未保存の編集含む)",
            Fields = new[]
            {
                new FieldDefinition
                {
                    Id = Guid.NewGuid(),
                    CsvColumn = "氏名",
                    X = 5, Y = 10,
                    FontFamily = "Yu Gothic", FontSizePt = 11, Color = "#000000",
                    Align = TextAlign.Left
                }
            }
        };

        var saved = _sut.SaveAsNewTemplate(created.TemplateId, editedLayout);

        Assert.NotEqual(created.TemplateId, saved.TemplateId);
        Assert.Equal("複製先(未保存の編集含む)", saved.TemplateName);
        Assert.Single(saved.Fields);
        Assert.True(File.Exists(_sut.GetPdfPath(saved.TemplateId)));

        // 複製元は一切変更されていないこと(未保存の編集が複製元に漏れていないこと)を確認する。
        var reloadedOriginal = _sut.GetLayout(created.TemplateId);
        Assert.Equal("複製元", reloadedOriginal.TemplateName);
        Assert.Empty(reloadedOriginal.Fields);
    }

    [Fact]
    public void SaveAsNewTemplate_テンプレート名が空の場合は例外を投げる()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);
        var created = _sut.CreateTemplate("元テンプレート", pdfStream);
        var editedLayout = created with { TemplateName = "   " };

        Assert.Throws<ArgumentException>(() => _sut.SaveAsNewTemplate(created.TemplateId, editedLayout));
    }

    [Fact]
    public void ReplacePdf_新しいPDFのページサイズをlayoutに反映する()
    {
        string originalPdfPath = TestPdfFactory.CreateBlankSinglePagePdf(widthPt: 400, heightPt: 500);
        using var originalStream = File.OpenRead(originalPdfPath);
        var created = _sut.CreateTemplate("差し替え対象", originalStream);
        Assert.Equal(400, created.PageSize.WidthPt, precision: 1);

        string newPdfPath = TestPdfFactory.CreateBlankSinglePagePdf(widthPt: 300, heightPt: 200);
        using var newStream = File.OpenRead(newPdfPath);

        var updated = _sut.ReplacePdf(created.TemplateId, newStream);

        Assert.Equal(300, updated.PageSize.WidthPt, precision: 1);
        Assert.Equal(200, updated.PageSize.HeightPt, precision: 1);
    }

    [Fact]
    public void ReplacePdf_不正なPDFの場合は例外を投げ元のPDFは壊さない()
    {
        string pdfPath = TestPdfFactory.CreateBlankSinglePagePdf();
        using var pdfStream = File.OpenRead(pdfPath);
        var created = _sut.CreateTemplate("差し替え失敗テスト", pdfStream);
        byte[] originalBytes = File.ReadAllBytes(_sut.GetPdfPath(created.TemplateId));

        using var invalidStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        Assert.ThrowsAny<Exception>(() => _sut.ReplacePdf(created.TemplateId, invalidStream));

        byte[] afterBytes = File.ReadAllBytes(_sut.GetPdfPath(created.TemplateId));
        Assert.Equal(originalBytes, afterBytes);
    }
}
