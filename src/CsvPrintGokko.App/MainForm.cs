using System.Diagnostics;
using System.Drawing;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CsvPrintGokko.App;

/// <summary>
/// アプリ本体のUIをWebView2(Chromiumベースの組み込みブラウザコントロール)でホストする専用ウィンドウ。
/// 既定ブラウザを開く方式に代えることで、ユーザー環境のブラウザ拡張機能やバージョン差異に
/// 表示が左右されないようにする。
/// </summary>
public sealed class MainForm : Form
{
    // ※外部(Microsoft公式)URLを開く箇所。WebView2ランタイムが見つからない場合のみ、
    // ユーザーが「はい」を選んだときにブラウザで開く(アプリ自身がネットワーク通信するわけではない)。
    private const string WebView2RuntimeDownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private readonly string _baseUrl;
    private readonly WebView2 _webView;

    public MainForm(string baseUrl)
    {
        _baseUrl = baseUrl;

        Text = "CSVペタっとPDF";
        Width = 1280;
        Height = 800;
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        try
        {
            // exeに埋め込み済みのアイコン(ApplicationIcon)をウィンドウ・タスクバー表示にも流用する。
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // アイコン取得に失敗しても起動自体は継続する(既定アイコンで表示されるだけ)。
        }

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        Load += MainForm_LoadAsync;
    }

    private async void MainForm_LoadAsync(object? sender, EventArgs e)
    {
        try
        {
            // 自己完結型シングルファイルexeは読み取り専用フォルダから実行され得るため、
            // WebView2のユーザーデータフォルダは書き込みが保証されたLocalApplicationData配下に明示する。
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CsvPrintGokko", "WebView2UserData");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.Navigate(_baseUrl);
        }
        catch (Exception ex)
        {
            // WebView2 Evergreenランタイムが未インストール等の場合にここに来る。
            // exeはコンソールを持たないが、リダイレクトされていれば調査用に詳細を出力する。
            Console.Error.WriteLine($"WebView2の初期化に失敗しました: {ex}");
            ShowWebView2InitializationError(ex);
            Close();
        }
    }

    /// <summary>
    /// WebView2ランタイムが見つからない等で初期化に失敗した場合、原因を案内したうえで、
    /// 必要であればユーザーの同意を得てからMicrosoft公式のランタイム配布ページを開く。
    /// </summary>
    private static void ShowWebView2InitializationError(Exception ex)
    {
        string message =
            "画面表示に必要なコンポーネント(Microsoft Edge WebView2 ランタイム)が見つかりませんでした。\n" +
            "多くのWindows環境には標準で入っていますが、お使いの環境には無いようです。\n\n" +
            $"詳細: {ex.Message}\n\n" +
            "「はい」を選ぶと、Microsoft公式のダウンロードページをブラウザで開きます。";

        var result = MessageBox.Show(message, "CSVペタっとPDF - 起動エラー",
            MessageBoxButtons.YesNo, MessageBoxIcon.Error);

        if (result == DialogResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo(WebView2RuntimeDownloadUrl) { UseShellExecute = true });
            }
            catch
            {
                // ブラウザ起動に失敗しても、これ以上アプリ側でできることはないため何もしない。
            }
        }
    }
}
