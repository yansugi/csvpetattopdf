using System.Text.Json;
using System.Text.Json.Serialization;
using CsvPrintGokko.Core.Models;

namespace CsvPrintGokko.Core.Json;

/// <summary>
/// CsvEncodingを.NETの Encoding.GetEncoding に渡せる名前("shift_jis"/"utf-8")として
/// そのままJSONに読み書きするコンバータ。変換ロジック本体は<see cref="CsvEncodingExtensions"/>を使う。
/// </summary>
public sealed class CsvEncodingJsonConverter : JsonConverter<CsvEncoding>
{
    public override CsvEncoding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        try
        {
            return CsvEncodingExtensions.ParseCsvEncoding(value ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException(ex.Message);
        }
    }

    public override void Write(Utf8JsonWriter writer, CsvEncoding value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToWireString());
    }
}
