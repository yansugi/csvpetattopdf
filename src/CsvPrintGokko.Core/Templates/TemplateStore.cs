using System.IO.Compression;
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
    public TemplateLayout CreateTemplate(string templateName, Stream pdfContent, TemplateKind kind = TemplateKind.Single)
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
            OutputSettings = new OutputSettings { Mode = OutputMode.Combined, FilenamePattern = "output_{row}.pdf" },
            Kind = kind,
            ListSettings = new ListRenderSettings()
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

    /// <summary>テンプレートをPDF・layout.jsonごと完全に削除する(元に戻せない)。</summary>
    public void DeleteTemplate(Guid templateId)
    {
        string dir = GetTemplateDirectory(templateId);
        if (!Directory.Exists(dir))
            throw new FileNotFoundException($"テンプレートが見つかりません: {templateId}");
        Directory.Delete(dir, recursive: true);
    }

    /// <summary>
    /// 現在編集中のレイアウト内容(未保存の変更を含む)を、PDF実体ごと新しいテンプレートとして
    /// 複製保存する(「名前を付けて保存」)。複製元のテンプレートには一切手を加えない。
    /// </summary>
    public TemplateLayout SaveAsNewTemplate(Guid sourceTemplateId, TemplateLayout editedLayout)
    {
        if (string.IsNullOrWhiteSpace(editedLayout.TemplateName))
            throw new ArgumentException("テンプレート名を指定してください。", nameof(editedLayout));

        string sourceDir = GetTemplateDirectory(sourceTemplateId);
        var sourceLayout = GetLayout(sourceTemplateId);
        string sourcePdfPath = Path.Combine(sourceDir, sourceLayout.PdfFileName);
        if (!File.Exists(sourcePdfPath))
            throw new FileNotFoundException("複製元のPDFが見つかりません。");

        var newTemplateId = Guid.NewGuid();
        string newDir = GetTemplateDirectory(newTemplateId);
        Directory.CreateDirectory(newDir);

        string newPdfPath = Path.Combine(newDir, sourceLayout.PdfFileName);
        File.Copy(sourcePdfPath, newPdfPath);

        // CSVが複製元テンプレート専用フォルダ内(インポート由来など)にある場合は一緒に複製し、
        // パスも新フォルダを指すよう書き換える。フォルダ外の任意のパスはそのまま共有参照する。
        string? newCsvPath = editedLayout.CsvSettings.LastFilePath;
        if (newCsvPath is not null && File.Exists(newCsvPath))
        {
            string fullSourceDir = Path.GetFullPath(sourceDir);
            string fullCsvDir = Path.GetFullPath(Path.GetDirectoryName(newCsvPath)!);
            if (string.Equals(fullCsvDir, fullSourceDir, StringComparison.OrdinalIgnoreCase))
            {
                string copiedCsvPath = Path.Combine(newDir, Path.GetFileName(newCsvPath));
                File.Copy(newCsvPath, copiedCsvPath);
                newCsvPath = copiedCsvPath;
            }
        }

        var now = DateTime.UtcNow;
        var newLayout = editedLayout with
        {
            TemplateId = newTemplateId,
            PdfFileName = sourceLayout.PdfFileName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CsvSettings = editedLayout.CsvSettings with { LastFilePath = newCsvPath }
        };

        return SaveLayout(newLayout);
    }

    /// <summary>
    /// 既存テンプレートのPDF実体を新しいPDFに差し替える。ページサイズは新PDFの1ページ目から
    /// 再取得してlayoutに反映する(既存フィールドの座標自体は変更しないため、ページサイズが
    /// 変わった場合の見た目の調整は呼び出し側=UIの responsibility とする)。
    /// アップロードされたPDFが不正な場合、元のPDFファイルを壊さないよう一時ファイル経由で検証する。
    /// </summary>
    public TemplateLayout ReplacePdf(Guid templateId, Stream pdfContent)
    {
        var layout = GetLayout(templateId);
        string dir = GetTemplateDirectory(templateId);
        string pdfPath = Path.Combine(dir, layout.PdfFileName);
        string tempPath = pdfPath + ".tmp";

        using (var fileStream = File.Create(tempPath))
        {
            pdfContent.CopyTo(fileStream);
        }

        PageSize newPageSize;
        try
        {
            newPageSize = ReadFirstPageSize(tempPath);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }

        File.Copy(tempPath, pdfPath, overwrite: true);
        File.Delete(tempPath);

        var updated = layout with { PageSize = newPageSize };
        return SaveLayout(updated);
    }

    private const string ProjectPdfEntryName = "template.pdf";
    private const string ProjectLayoutEntryName = "layout.json";
    private const string ProjectCsvEntryName = "data.csv";

    /// <summary>
    /// テンプレート(PDF+layout.json)と、最後に読み込んだCSV(存在すれば)を1つのzipにまとめて返す。
    /// 別の環境へそのまま持ち出し、ImportProjectで読み込めるようにするための書き出し。
    /// CSVファイルが見つからない(移動・削除済み)場合はCSV無しで書き出し、layout.json内の
    /// LastFilePathはnullにしておく(インポート先に存在しないパスを自動読み込みしようとして
    /// エラーになるのを防ぐため)。
    /// </summary>
    public byte[] ExportProject(Guid templateId)
    {
        var layout = GetLayout(templateId);
        string dir = GetTemplateDirectory(templateId);
        string pdfPath = Path.Combine(dir, layout.PdfFileName);

        string? csvPath = layout.CsvSettings.LastFilePath;
        bool csvExists = csvPath is not null && File.Exists(csvPath);

        var exportLayout = layout with
        {
            CsvSettings = layout.CsvSettings with { LastFilePath = csvExists ? ProjectCsvEntryName : null }
        };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var layoutEntry = archive.CreateEntry(ProjectLayoutEntryName);
            using (var writer = new StreamWriter(layoutEntry.Open()))
                writer.Write(JsonSerializer.Serialize(exportLayout, JsonDefaults.Options));

            archive.CreateEntryFromFile(pdfPath, ProjectPdfEntryName);

            if (csvExists)
                archive.CreateEntryFromFile(csvPath!, ProjectCsvEntryName);
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// ExportProjectで作成したzipから新規テンプレートとしてインポートする。
    /// テンプレートIDは常に新規採番する(インポート元・既存テンプレートとのID衝突を避けるため)。
    /// CSVが同梱されていればテンプレート専用フォルダ内に保存し、LastFilePathをその絶対パスに
    /// 書き換えることで、配置エディタの「前回CSVの自動読込」がそのまま機能するようにする。
    /// </summary>
    public TemplateLayout ImportProject(Stream zipContent)
    {
        using var archive = new ZipArchive(zipContent, ZipArchiveMode.Read);

        var layoutEntry = archive.GetEntry(ProjectLayoutEntryName)
            ?? throw new InvalidDataException("プロジェクトファイルにlayout.jsonが含まれていません。");
        var pdfEntry = archive.GetEntry(ProjectPdfEntryName)
            ?? throw new InvalidDataException("プロジェクトファイルにtemplate.pdfが含まれていません。");

        TemplateLayout importedLayout;
        using (var reader = new StreamReader(layoutEntry.Open()))
        {
            string json = reader.ReadToEnd();
            importedLayout = JsonSerializer.Deserialize<TemplateLayout>(json, JsonDefaults.Options)
                ?? throw new InvalidDataException("layout.jsonの形式が不正です。");
        }

        var newTemplateId = Guid.NewGuid();
        string dir = GetTemplateDirectory(newTemplateId);
        Directory.CreateDirectory(dir);

        using (var pdfEntryStream = pdfEntry.Open())
        using (var pdfFileStream = File.Create(Path.Combine(dir, ProjectPdfEntryName)))
        {
            pdfEntryStream.CopyTo(pdfFileStream);
        }

        string? newCsvPath = null;
        var csvEntry = archive.GetEntry(ProjectCsvEntryName);
        if (csvEntry is not null)
        {
            newCsvPath = Path.Combine(dir, ProjectCsvEntryName);
            using var csvEntryStream = csvEntry.Open();
            using var csvFileStream = File.Create(newCsvPath);
            csvEntryStream.CopyTo(csvFileStream);
        }

        var now = DateTime.UtcNow;
        var finalLayout = importedLayout with
        {
            TemplateId = newTemplateId,
            PdfFileName = ProjectPdfEntryName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CsvSettings = importedLayout.CsvSettings with { LastFilePath = newCsvPath }
        };

        return SaveLayout(finalLayout);
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
