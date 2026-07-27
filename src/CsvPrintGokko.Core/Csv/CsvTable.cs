namespace CsvPrintGokko.Core.Csv;

/// <summary>CSVファイルをパースした結果。各行は列名をキーとする辞書として保持する。</summary>
public sealed class CsvTable
{
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; }
}
