namespace CsvPrintGokko.Core.Models;

/// <summary>
/// CsvEncodingと、.NETのEncoding.GetEncodingにもJSON/APIの値としても使う
/// 文字列表現("shift_jis"/"utf-8")との相互変換。JSONシリアライズとHTTPフォームの
/// 両方から同じ変換ロジックを共有するために切り出している。
/// </summary>
public static class CsvEncodingExtensions
{
    public static CsvEncoding ParseCsvEncoding(string value) => value switch
    {
        "shift_jis" => CsvEncoding.ShiftJis,
        "utf-8" => CsvEncoding.Utf8,
        _ => throw new ArgumentException($"不明なCSVエンコーディングです: {value}")
    };

    public static string ToWireString(this CsvEncoding encoding) => encoding switch
    {
        CsvEncoding.ShiftJis => "shift_jis",
        CsvEncoding.Utf8 => "utf-8",
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };
}
