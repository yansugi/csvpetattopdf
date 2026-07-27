using System.Text.Json;
using CsvPrintGokko.Core.Json;
using CsvPrintGokko.Core.Models;
using PdfSharp.Pdf.IO;

namespace CsvPrintGokko.Core.Templates;

/// <summary>
/// テンプレート(PDF実体+layout.json)を %LOCALAPPDATA%\CsvPrintGokko\Templates\{templateId}\ 配下に
/// 永続化するストア。テンプレート数は多くても数十件程度を想定しており、
/// 別途インデックスは持たずフォルダ列挙のみで一覧を構成する。
/// </summary>
public sealed class TemplateStore
{
    private const string LayoutFileName = "layout.json";
    private readonly string _rootDirectory;

    public TemplateStore() : this(GetDefaultRootDirectory())
    {
    }

    public TemplateStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    private static string GetDefaultRootDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "CsvPrintGokko", "Templates");
    }

    /// <summary>
    /// 新規テンプレートを作成する。アップロードされたPDFを保存し、1ページ目のサイズから
    /// 既定のcsvSettings/outputSettingsを持つlayout.jsonを生成する。
    /// </summary>
    public TemplateLayout CreateTemplate(string templateName, Stream pdfContent)
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("テンプレート名を指定してください。", nameof(templateName));
        ArgumentNullException.ThrowIfNull(pdfContent);

        var templateId = Guid.NewGuid();
        string dir = GetTemplateDirectory(templateId);
        Directory.CreateDirectory(dir);

        string pdfPath = Path.Combine(dir, "template.pdf");
        using (var fileStream = File.Create(pdfPath))
        {
            pdfContent.CopyTo(fileStream);
        }

        PageSize pageSize = ReadFirstPageSize(pdfPath);
        var now = DateTime.UtcNow;

        var layout = new TemplateLayout
        {
            TemplateId = templateId,
            TemplateName = templateName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            PdfFileName = "template.pdf",
            PageSize = pageSize,
            CsvSettings = new CsvSettings { Encoding = CsvEncoding.Utf8, Delimiter = ",", HasHeader = true },
            Fields = Array.Empty<FieldDefinition>(),
            OutputSettings = new OutputSettings { Mode = OutputMode.Combined, FilenamePattern = "output_{row}.pdf" }
        };

        return SaveLayout(layout);
    }

    /// <summary>保存済みの全テンプレートを更新日時の新しい順に列挙する(ホーム画面用)。</summary>
    public IReadOnlyList<TemplateLayout> ListTemplates()
    {
        if (!Directory.Exists(_rootDirectory))
            return Array.Empty<TemplateLayout>();

        var results = new List<TemplateLayout>();
        foreach (string dir in Directory.EnumerateDirectories(_rootDirectory))
        {
            string layoutPath = Path.Combine(dir, LayoutFileName);
            if (!File.Exists(layoutPath))
                continue;

            try
            {
                results.Add(LoadLayoutFromFile(layoutPath));
            }
            catch (JsonException)
            {
                // 壊れたlayout.jsonは一覧から除外する。個別のテンプレート破損で全体が
                // 使えなくなる事態を避けるための割り切り。
            }
        }
        return results.OrderByDescending(t => t.UpdatedAtUtc).ToList();
    }

    /// <summary>テンプレートIDからレイアウトを取得する。</summary>
    public TemplateLayout GetLayout(Guid templateId)
    {
        string layoutPath = GetLayoutPath(templateId);
        if (!File.Exists(layoutPath))
            throw new FileNotFoundException($"テンプレートが見つかりません: {templateId}");
        return LoadLayoutFromFile(layoutPath);
    }

    /// <summary>レイアウトを保存する。保存の都度、更新日時を現在時刻(UTC)に更新する。</summary>
    public TemplateLayout SaveLayout(TemplateLayout layout)
    {
        var updated = layout with { UpdatedAtUtc = DateTime.UtcNow };
        string dir = GetTemplateDirectory(updated.TemplateId);
        Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(updated, JsonDefaults.Options);
        File.WriteAllText(GetLayoutPath(updated.TemplateId), json);
        return updated;
    }

    /// <summary>テンプレートPDF実体のフルパスを返す(pdf.js配信やPDF合成で使用)。</summary>
    public string GetPdfPath(Guid templateId)
    {
        var layout = GetLayout(templateId);
        return Path.Combine(GetTemplateDirectory(templateId), layout.PdfFileName);
    }

    private string GetTemplateDirectory(Guid templateId) => Path.Combine(_rootDirectory, templateId.ToString());

    private string GetLayoutPath(Guid templateId) => Path.Combine(GetTemplateDirectory(templateId), LayoutFileName);

    private static TemplateLayout LoadLayoutFromFile(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TemplateLayout>(json, JsonDefaults.Options)
            ?? throw new InvalidDataException($"layout.jsonの読み込みに失敗しました: {path}");
    }

    private static PageSize ReadFirstPageSize(string pdfPath)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        if (document.PageCount == 0)
            throw new InvalidDataException("PDFにページが含まれていません。");

        var page = document.Pages[0];
        return new PageSize { WidthPt = page.Width.Point, HeightPt = page.Height.Point };
    }
}
