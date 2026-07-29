using System.Diagnostics;
using System.Text;
using CsvPrintGokko.App.Endpoints;
using CsvPrintGokko.App.Services;
using CsvPrintGokko.Core.Jobs;
using CsvPrintGokko.Core.Json;
using CsvPrintGokko.Core.Templates;

// Shift-JIS等の日本語コードページを扱えるようにする(CSV読み込みで使用)。
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// ブラウザだけ閉じてバックグラウンドプロセスが残ってしまった場合、ポートを掴んだままだと
// 新しい起動がAddressInUseExceptionで失敗する。同じexeパスの残留プロセスが動いていれば、
// 誤って無関係な別ソフトを巻き込まないようパスを照合したうえで終了させてから起動を続ける。
KillLingeringInstances();

static void KillLingeringInstances()
{
    using var current = Process.GetCurrentProcess();
    string? currentPath = TryGetMainModulePath(current);
    if (currentPath is null)
        return;

    foreach (var proc in Process.GetProcessesByName(current.ProcessName))
    {
        using (proc)
        {
            if (proc.Id == current.Id)
                continue;

            string? otherPath = TryGetMainModulePath(proc);
            if (otherPath is null || !string.Equals(otherPath, currentPath, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                proc.Kill();
                proc.WaitForExit(5000);
            }
            catch
            {
                // 権限不足等で終了できない場合は諦める(ポートが空かなければ通常通り起動エラーになる)。
            }
        }
    }
}

static string? TryGetMainModulePath(Process process)
{
    try
    {
        return process.MainModule?.FileName;
    }
    catch
    {
        // 他ユーザー権限のプロセス等、アクセスできない場合は対象外として扱う。
        return null;
    }
}

// ローカル完結型Webアプリのため、外部からアクセスされないようループバックに固定する。
const string BaseUrl = "http://127.0.0.1:48923";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(BaseUrl);
builder.Services.AddSingleton<TemplateStore>();
builder.Services.AddSingleton<OutputJobRunner>();
builder.Services.AddSingleton<StaFolderDialogService>();
builder.Services.AddSingleton<HeartbeatTracker>();
builder.Services.AddHostedService<HeartbeatWatchdogService>();
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
app.MapHeartbeatEndpoints();

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
