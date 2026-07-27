using System.Globalization;
using CsvPrintGokko.Core.Models;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// FieldDefinition.DataTypeに応じてCSVの生値を表示用文字列に整形する。
/// PdfComposerService(PDF描画)とeditor.jsのプレビュー(preview/renderエンドポイント経由)の
/// 両方から同じ結果を得るため、整形ロジックはPDF描画処理と分離してここに集約する。
/// </summary>
public static class CsvValueFormatter
{
    private static readonly CultureInfo JapaneseEraCulture = CreateJapaneseEraCulture();

    private static CultureInfo CreateJapaneseEraCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("ja-JP").Clone();
        culture.DateTimeFormat.Calendar = new JapaneseCalendar();
        return culture;
    }

    /// <summary>rawValueをfield.DataTypeに従って整形する。解析に失敗した場合は元の文字列をそのまま返す。</summary>
    public static string Format(FieldDefinition field, string rawValue)
    {
        return field.DataType switch
        {
            DataType.Date => FormatDate(field, rawValue),
            DataType.Number => FormatNumber(field, rawValue),
            DataType.Boolean => FormatBoolean(field, rawValue),
            _ => rawValue
        };
    }

    private static string FormatDate(FieldDefinition field, string rawValue)
    {
        if (!DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
            !DateTime.TryParse(rawValue, CultureInfo.GetCultureInfo("ja-JP"), DateTimeStyles.None, out date))
        {
            return rawValue;
        }

        return field.DateFormatKind switch
        {
            DateFormatKind.Kanji => date.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture),
            DateFormatKind.MonthDay => date.ToString("MM/dd", CultureInfo.InvariantCulture),
            DateFormatKind.Japanese => date.ToString("ggy年M月d日", JapaneseEraCulture),
            DateFormatKind.SlashWithTime => date.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
            DateFormatKind.KanjiWithTime => date.ToString("yyyy年MM月dd日 HH時mm分", CultureInfo.InvariantCulture),
            DateFormatKind.TimeOnly => date.ToString("HH:mm", CultureInfo.InvariantCulture),
            DateFormatKind.JapaneseWithTime => date.ToString("ggy年M月d日 HH時mm分", JapaneseEraCulture),
            DateFormatKind.Custom => date.ToString(
                string.IsNullOrEmpty(field.DateCustomFormat) ? "yyyy/MM/dd" : field.DateCustomFormat,
                CultureInfo.InvariantCulture),
            _ => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture)
        };
    }

    private static string FormatNumber(FieldDefinition field, string rawValue)
    {
        if (!double.TryParse(rawValue, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return rawValue;
        }

        return FormatNumberValue(field, value);
    }

    /// <summary>数値の桁区切り・小数桁数・接頭辞接尾辞をfieldの設定に従って適用する。計算フィールド(FieldKind.Calc)の結果表示にも使う。</summary>
    public static string FormatNumberValue(FieldDefinition field, double value)
    {
        string numberFormat = field.NumberDecimalPlaces switch
        {
            int decimals when decimals >= 0 => field.NumberUseThousandsSeparator ? $"N{decimals}" : $"F{decimals}",
            _ => field.NumberUseThousandsSeparator ? "#,0.################" : "0.################"
        };

        return $"{field.NumberPrefix}{value.ToString(numberFormat, CultureInfo.InvariantCulture)}{field.NumberSuffix}";
    }

    private static string FormatBoolean(FieldDefinition field, string rawValue)
    {
        var trueValues = field.BooleanTrueValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool isTrue = trueValues.Any(v => string.Equals(v, rawValue.Trim(), StringComparison.OrdinalIgnoreCase));
        return isTrue ? field.BooleanTrueDisplay : field.BooleanFalseDisplay;
    }
}
