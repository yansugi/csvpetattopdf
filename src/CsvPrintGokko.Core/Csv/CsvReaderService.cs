using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvPrintGokko.Core.Models;

namespace CsvPrintGokko.Core.Csv;

/// <summary>
/// CsvSettings(エンコーディング/区切り文字/ヘッダー有無)に従ってCSVをパースするサービス。
/// </summary>
public sealed class CsvReaderService
{
    static CsvReaderService()
    {
        // Shift-JIS等の日本語コードページを扱えるようにする。複数回登録しても問題ない。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvTable Read(Stream csvContent, CsvSettings settings)
    {
        var encoding = ResolveEncoding(settings.Encoding);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = settings.Delimiter,
            HasHeaderRecord = settings.HasHeader
        };

        using var reader = new StreamReader(csvContent, encoding);
        using var csv = new CsvReader(reader, config);

        return settings.HasHeader ? ReadWithHeader(csv) : ReadWithoutHeader(csv);
    }

    private static CsvTable ReadWithHeader(CsvReader csv)
    {
        if (!csv.Read())
            return new CsvTable { Headers = Array.Empty<string>(), Rows = Array.Empty<IReadOnlyDictionary<string, string>>() };

        csv.ReadHeader();
        var headers = csv.HeaderRecord?.ToList()
            ?? throw new InvalidDataException("CSVのヘッダー行を読み取れませんでした。");

        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (csv.Read())
        {
            var row = new Dictionary<string, string>();
            foreach (string header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }
            rows.Add(row);
        }

        return new CsvTable { Headers = headers, Rows = rows };
    }

    private static CsvTable ReadWithoutHeader(CsvReader csv)
    {
        var headers = new List<string>();
        var rows = new List<IReadOnlyDictionary<string, string>>();

        while (csv.Read())
        {
            if (headers.Count == 0)
            {
                // ヘッダー無しの場合は列名を "列1","列2"... のように自動採番する。
                for (int i = 0; i < csv.Parser.Count; i++)
                    headers.Add($"列{i + 1}");
            }

            var row = new Dictionary<string, string>();
            for (int i = 0; i < headers.Count; i++)
                row[headers[i]] = csv.GetField(i) ?? string.Empty;
            rows.Add(row);
        }

        return new CsvTable { Headers = headers, Rows = rows };
    }

    private static Encoding ResolveEncoding(CsvEncoding encoding) => encoding switch
    {
        CsvEncoding.ShiftJis => Encoding.GetEncoding("shift_jis"),
        CsvEncoding.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "未対応のCSVエンコーディングです。")
    };
}
