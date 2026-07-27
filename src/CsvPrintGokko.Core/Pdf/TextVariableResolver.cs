using System.Text.RegularExpressions;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// 自由テキスト(FieldKind.Text)内の"{列名}"をCSVの実データに置換する。
/// 出力ファイル名パターン(FileNamePatternService)と同じ"{列名}"記法を使い、
/// アプリ全体で変数記法の見え方を統一する。
/// </summary>
public static partial class TextVariableResolver
{
    /// <summary>templateの"{列名}"をrowDataの値に置換する。対応する列が無い場合は空文字にする。</summary>
    public static string Resolve(string template, IReadOnlyDictionary<string, string> rowData)
    {
        return TokenPattern().Replace(template, match =>
        {
            string column = match.Groups[1].Value;
            return rowData.TryGetValue(column, out var value) ? value : string.Empty;
        });
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenPattern();
}
