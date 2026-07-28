using System.Globalization;
using System.Text.RegularExpressions;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// 自由テキスト(FieldKind.Text)内の"{列名}"をCSVの実データに、"{行番号}"を1始まりの行番号に置換する。
/// 出力ファイル名パターン(FileNamePatternService)や計算式(FormulaEvaluator)と同じ"{列名}"記法を使い、
/// アプリ全体で変数記法の見え方を統一する。
/// </summary>
public static partial class TextVariableResolver
{
    private const string RowNumberToken = "行番号";

    /// <summary>templateの"{列名}"をrowDataの値に、"{行番号}"をrowNumberに置換する。対応する列が無い場合は空文字にする。</summary>
    public static string Resolve(string template, IReadOnlyDictionary<string, string> rowData, int rowNumber)
    {
        return TokenPattern().Replace(template, match =>
        {
            string column = match.Groups[1].Value;
            if (column == RowNumberToken)
                return rowNumber.ToString(CultureInfo.InvariantCulture);
            return rowData.TryGetValue(column, out var value) ? value : string.Empty;
        });
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenPattern();
}
