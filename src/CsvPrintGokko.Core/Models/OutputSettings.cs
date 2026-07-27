namespace CsvPrintGokko.Core.Models;

/// <summary>PDF出力時の既定設定。テンプレートに紐づけて保存・再利用する。</summary>
public sealed record OutputSettings
{
    public required OutputMode Mode { get; init; }
    public required string FilenamePattern { get; init; }
}
