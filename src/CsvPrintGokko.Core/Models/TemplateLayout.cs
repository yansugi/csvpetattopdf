namespace CsvPrintGokko.Core.Models;

/// <summary>
/// テンプレート1件分の配置レイアウト設定。PDF実体と対で永続化される(layout.json)。
/// </summary>
public sealed record TemplateLayout
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid TemplateId { get; init; }
    public required string TemplateName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string PdfFileName { get; init; }
    public required PageSize PageSize { get; init; }
    public required CsvSettings CsvSettings { get; init; }
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = Array.Empty<FieldDefinition>();
    public required OutputSettings OutputSettings { get; init; }

    /// <summary>テンプレートの種類。Single(差込印刷、CSV1行=1ページ)/List(一覧表示、全行を1つの一覧表にまとめる)。</summary>
    public TemplateKind Kind { get; init; } = TemplateKind.Single;

    /// <summary>Kind=Listのときの一覧表示設定(行の高さ・ゼブラ縞など)。Kind=Singleでは未使用。</summary>
    public ListRenderSettings ListSettings { get; init; } = new();
}
