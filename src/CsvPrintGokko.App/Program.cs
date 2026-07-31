using System.Diagnostics;
using System.Text;
using CsvPrintGokko.App.Endpoints;
using CsvPrintGokko.App.Services;
using CsvPrintGokko.Core.Jobs;
using CsvPrintGokko.Core.Json;
using CsvPrintGokko.Core.Templates;

namespace CsvPrintGokko.App;

internal static class Program
{
    // ローカル完結型Webアプリのため、外部からアクセスされないようループバックに固定する。
    private const string BaseUrl = "http://127.0.0.1:48923";

    // WebView2はCOMのSTAスレッドを要求する。トップレベルステートメントのMainには
    // [STAThread]を直接付与できず、Microsoft.NET.Sdk.Web + UseWindowsFormsの組み合わせでは
    // 自動付与もされない(既定でMTAスレッドとして起動してしまう)ため、明示的なMainメソッドに
    // [STAThread]を付けて確実にSTAスレッドで起動する。
    [STAThread]
    private static void Main(string[] args)
    {
        // Shift-JIS等の日本語コードページを扱えるようにする(CSV読み込みで使用)。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // ブラウザだけ閉じてバックグラウンドプロセスが残ってしまった場合、ポートを掴んだままだと
        // 新しい起動がAddressInUseExceptionで失敗する。同じexeパスの残留プロセスが動いていれば、
        // 誤って無関係な別ソフトを巻き込まないようパスを照合したうえで終了させてから起動を続ける。
        KillLingeringInstances();

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

        // UIは既定のブラウザではなく、WebView2を埋め込んだ専用ウィンドウで表示する。
        // Kestrelは同期APIで起動し、メインスレッドはWinFormsのメッセージループに専念させる
        // (`await`を挟むと継続処理がスレッドプール上で走り得るためSTA状態を維持できなくなる)。
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();

        app.Start();

        using var mainForm = new MainForm(BaseUrl);
        mainForm.FormClosed += (_, _) =>
        {
            // UIスレッド(WindowsFormsSynchronizationContext)上でStopAsync()を直接同期待機すると、
            // 内部の継続処理がこのスレッドのメッセージポンプに戻れずデッドロックし得る。
            // 同期コンテキストを持たないThreadPoolスレッドで実行することでこれを避ける。
            Task.Run(() => app.StopAsync()).GetAwaiter().GetResult();
        };
        Application.Run(mainForm);

        app.WaitForShutdown();
    }

    private static void KillLingeringInstances()
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

    private static string? TryGetMainModulePath(Process process)
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
}
