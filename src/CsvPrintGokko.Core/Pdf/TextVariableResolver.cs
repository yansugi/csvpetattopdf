using System.Globalization;
using System.Text.RegularExpressions;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// 自由テキスト(FieldKind.Text)内の"{列名}"をCSVの実データに、"{行番号}"を1始まりのCSV行番号に、
/// "{ページ番号}"を現在のページ番号に、"{総ページ数}"を出力全体の総ページ数に、"{出力時間}"を出力実行時刻に置換する。
/// 出力ファイル名パターン(FileNamePatternService)や計算式(FormulaEvaluator)と同じ"{列名}"記法を使い、
/// アプリ全体で変数記法の見え方を統一する。
/// </summary>
public static partial class TextVariableResolver
{
    private const string RowNumberToken = "行番号";
    private const string PageNumberToken = "ページ番号";
    private const string TotalPageCountToken = "総ページ数";
    private const string OutputDateTimeToken = "出力時間";

    /// <summary>
    /// templateの"{列名}"をrowDataの値に、"{行番号}"をrowNumberに、"{ページ番号}"をpageNumberに、
    /// "{総ページ数}"をtotalPageCountに、"{出力時間}"をoutputDateTimeに置換する。対応する列が無い場合は空文字にする。
    /// </summary>
    public static string Resolve(string template, IReadOnlyDictionary<string, string> rowData, int rowNumber, int pageNumber, int totalPageCount, string outputDateTime)
    {
        return TokenPattern().Replace(template, match =>
        {
            string column = match.Groups[1].Value;
            if (column == RowNumberToken)
                return rowNumber.ToString(CultureInfo.InvariantCulture);
            if (column == PageNumberToken)
                return pageNumber.ToString(CultureInfo.InvariantCulture);
            if (column == TotalPageCountToken)
                return totalPageCount.ToString(CultureInfo.InvariantCulture);
            if (column == OutputDateTimeToken)
                return outputDateTime;
            return rowData.TryGetValue(column, out var value) ? value : string.Empty;
        });
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenPattern();
}
