using System.Globalization;
using System.Text.RegularExpressions;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// FieldDefinition.Formula(例: "{単価}*{数量}")を評価する。
/// "{列名}"はrowDataの値、"{行番号}"は1始まりのCSV行番号、"{ページ番号}"は現在のページ番号、
/// "{総ページ数}"は出力全体の総ページ数に置換したうえで、+ - * / ( ) からなる四則演算式として計算する。
/// 参照列が無い・数値でない・0除算・構文エラーなどの場合はTryEvaluateがfalseを返す。
/// </summary>
public static partial class FormulaEvaluator
{
    private const string RowNumberToken = "行番号";
    private const string PageNumberToken = "ページ番号";
    private const string TotalPageCountToken = "総ページ数";

    public static bool TryEvaluate(string formula, IReadOnlyDictionary<string, string> rowData, int rowNumber, int pageNumber, int totalPageCount, out double result)
    {
        result = 0;
        string? substituted = SubstituteVariables(formula, rowData, rowNumber, pageNumber, totalPageCount);
        if (substituted is null)
            return false;

        try
        {
            int pos = 0;
            result = ParseExpression(substituted, ref pos);
            SkipWhitespace(substituted, ref pos);
            return pos == substituted.Length;
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException)
        {
            result = 0;
            return false;
        }
    }

    /// <summary>"{列名}"/"{行番号}"/"{ページ番号}"/"{総ページ数}"を実際の値に置換する。対応する列が無い場合はnullを返す。</summary>
    private static string? SubstituteVariables(string formula, IReadOnlyDictionary<string, string> rowData, int rowNumber, int pageNumber, int totalPageCount)
    {
        bool missingColumn = false;
        string replaced = TokenPattern().Replace(formula, match =>
        {
            string name = match.Groups[1].Value;
            if (name == RowNumberToken)
                return rowNumber.ToString(CultureInfo.InvariantCulture);
            if (name == PageNumberToken)
                return pageNumber.ToString(CultureInfo.InvariantCulture);
            if (name == TotalPageCountToken)
                return totalPageCount.ToString(CultureInfo.InvariantCulture);
            if (rowData.TryGetValue(name, out var value))
                return value;
            missingColumn = true;
            return "0";
        });
        return missingColumn ? null : replaced;
    }

    private static double ParseExpression(string s, ref int pos)
    {
        double value = ParseTerm(s, ref pos);
        while (true)
        {
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            {
                char op = s[pos];
                pos++;
                double rhs = ParseTerm(s, ref pos);
                value = op == '+' ? value + rhs : value - rhs;
            }
            else
            {
                break;
            }
        }
        return value;
    }

    private static double ParseTerm(string s, ref int pos)
    {
        double value = ParseFactor(s, ref pos);
        while (true)
        {
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && (s[pos] == '*' || s[pos] == '/'))
            {
                char op = s[pos];
                pos++;
                double rhs = ParseFactor(s, ref pos);
                if (op == '*')
                {
                    value *= rhs;
                }
                else
                {
                    if (rhs == 0)
                        throw new DivideByZeroException();
                    value /= rhs;
                }
            }
            else
            {
                break;
            }
        }
        return value;
    }

    private static double ParseFactor(string s, ref int pos)
    {
        SkipWhitespace(s, ref pos);
        if (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
        {
            char sign = s[pos];
            pos++;
            double value = ParseFactor(s, ref pos);
            return sign == '-' ? -value : value;
        }
        if (pos < s.Length && s[pos] == '(')
        {
            pos++;
            double value = ParseExpression(s, ref pos);
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length || s[pos] != ')')
                throw new FormatException("かっこが閉じていません。");
            pos++;
            return value;
        }

        int start = pos;
        while (pos < s.Length && (char.IsAsciiDigit(s[pos]) || s[pos] == '.'))
            pos++;
        if (pos == start)
            throw new FormatException("数値として解釈できない文字があります。");

        return double.Parse(s.AsSpan(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static void SkipWhitespace(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos]))
            pos++;
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenPattern();
}
