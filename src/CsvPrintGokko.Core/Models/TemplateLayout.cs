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
}
