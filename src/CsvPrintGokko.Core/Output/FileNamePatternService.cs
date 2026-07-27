using System.Text;
using System.Text.RegularExpressions;

namespace CsvPrintGokko.Core.Output;

/// <summary>
/// 出力ファイル名パターン(例: "{氏名}_{発行日}.pdf")を実データで解決し、
/// Windowsのファイル名として使えない文字のサニタイズと、重複時の連番付与を行う。
/// </summary>
public sealed partial class FileNamePatternService
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    /// <summary>パターン内の{列名}をrowDataの値に置換し、不正文字をサニタイズしたファイル名を返す。</summary>
    public string Resolve(string pattern, IReadOnlyDictionary<string, string> rowData)
    {
        string replaced = TokenPattern().Replace(pattern, match =>
        {
            string column = match.Groups[1].Value;
            return rowData.TryGetValue(column, out var value) ? value : string.Empty;
        });

        string sanitized = Sanitize(replaced);
        return sanitized.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? sanitized : sanitized + ".pdf";
    }

    /// <summary>同名ファイルが既に使われている場合、"name (2).pdf"のように連番を付与して重複を避ける。</summary>
    public string Deduplicate(string fileName, ISet<string> usedNames)
    {
        if (usedNames.Add(fileName))
            return fileName;

        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);

        int counter = 2;
        string candidate;
        do
        {
            candidate = $"{nameWithoutExt} ({counter}){ext}";
            counter++;
        } while (!usedNames.Add(candidate));

        return candidate;
    }

    private static string Sanitize(string fileName)
    {
        var builder = new StringBuilder(fileName.Length);
        foreach (char c in fileName)
            builder.Append(InvalidChars.Contains(c) ? '_' : c);

        string result = builder.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "output" : result;
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex TokenPattern();
}
