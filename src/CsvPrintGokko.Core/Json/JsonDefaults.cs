using System.Text.Json;
using System.Text.Json.Serialization;

namespace CsvPrintGokko.Core.Json;

/// <summary>
/// アプリ全体で共通利用するJSON設定。layout.jsonのファイル読み書き(<see cref="Options"/>)と
/// ASP.NET CoreのHTTP JSONシリアライズ(<see cref="Apply"/>)の両方に同じenum表現を適用し、
/// ファイル保存とAPIレスポンスで値の見え方が食い違わないようにする。
/// </summary>
public static class JsonDefaults
{
    /// <summary>enum(TextAlign等)は名前をすべて小文字にしたものをJSON表現とする。</summary>
    private sealed class LowerCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name.ToLowerInvariant();
    }

    public static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>既存のJsonSerializerOptions(ASP.NET CoreのHttpJsonOptions等)に同じ設定を追加する。</summary>
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new CsvEncodingJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter(new LowerCaseNamingPolicy()));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        Apply(options);
        return options;
    }
}
