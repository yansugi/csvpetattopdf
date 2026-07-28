using System.Text;
using CsvPrintGokko.App.Endpoints;
using CsvPrintGokko.App.Services;
using CsvPrintGokko.Core.Jobs;
using CsvPrintGokko.Core.Json;
using CsvPrintGokko.Core.Templates;

// Shift-JIS等の日本語コードページを扱えるようにする(CSV読み込みで使用)。
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// ローカル完結型Webアプリのため、外部からアクセスされないようループバックに固定する。
const string BaseUrl = "http://127.0.0.1:48923";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(BaseUrl);
builder.Services.AddSingleton<TemplateStore>();
builder.Services.AddSingleton<OutputJobRunner>();
builder.Services.AddSingleton<StaFolderDialogService>();
builder.Services.AddMemoryCache();
// layout.jsonのファイル保存と同じenum表現(小文字文字列)をAPIレスポンスにも適用する。
builder.Services.ConfigureHttpJsonOptions(options => JsonDefaults.Apply(options.SerializerOptions));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapTemplateEndpoints();
app.MapCsvEndpoints();
app.MapPreviewEndpoints();
app.MapOutputEndpoints();
app.MapDialogEndpoints();
app.MapFormulaEndpoints();

// 起動完了後に既定のブラウザでUIを自動的に開く。
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(BaseUrl)
        {
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"既定のブラウザを起動できませんでした。手動で {BaseUrl} を開いてください。({ex.Message})");
    }
});

app.Run();
