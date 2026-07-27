namespace CsvPrintGokko.Core.Models;

/// <summary>テンプレートに紐づくCSV読み込み設定。</summary>
public sealed record CsvSettings
{
    public required CsvEncoding Encoding { get; init; }
    public required string Delimiter { get; init; }
    public required bool HasHeader { get; init; }

    /// <summary>最後に読み込んだCSVファイルの絶対パス。プロジェクトを開き直した際の自動再読込に使う。</summary>
    public string? LastFilePath { get; init; }
}
