namespace CsvPrintGokko.Core.Models;

/// <summary>PDFページの寸法(pt単位)。</summary>
public sealed record PageSize
{
    public required double WidthPt { get; init; }
    public required double HeightPt { get; init; }
}
