using System.Runtime.InteropServices;

namespace CsvPrintGokko.App.Services;

/// <summary>
/// System.Windows.Forms.FolderBrowserDialog/OpenFileDialogを呼び出すサービス。
/// Phase 0のスパイク検証で判明した通り、非STAスレッド(Kestrelのリクエストハンドラスレッド等)から
/// ShowDialog()を呼んでも例外にならず、ユーザーに見えない/操作できないダイアログのまま
/// 無期限にハングし得る。そのため必ず専用のSTAスレッドを起動して呼び出し、
/// 呼び出し側にもタイムアウトを設けることで、誤操作時にリクエストが永久に固まるのを防ぐ。
/// また、このアプリは可視ウィンドウを持たないKestrelのバックグラウンドプロセスであるため、
/// TopMostを指定するだけではWindowsのフォアグラウンドロックにより前面化に失敗することがある。
/// AttachThreadInputで現在のフォアグラウンドスレッドと入力キューを一時的に結合することで、
/// SetForegroundWindowを確実に成功させる。
/// </summary>
public sealed class StaFolderDialogService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();


    public Task<string?> BrowseFolderAsync(string description, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string?>();

        var thread = new Thread(() =>
        {
            try
            {
                using var owner = CreateTopMostOwner();
                using var dialog = new FolderBrowserDialog
                {
                    Description = description,
                    ShowNewFolderButton = true
                };
                var result = dialog.ShowDialog(owner);
                tcs.TrySetResult(result == DialogResult.OK ? dialog.SelectedPath : null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return WithTimeout(tcs.Task, timeout);
    }

    public Task<string?> BrowseFileAsync(string title, string filter, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string?>();

        var thread = new Thread(() =>
        {
            try
            {
                using var owner = CreateTopMostOwner();
                using var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter,
                    CheckFileExists = true,
                    Multiselect = false
                };
                var result = dialog.ShowDialog(owner);
                tcs.TrySetResult(result == DialogResult.OK ? dialog.FileName : null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return WithTimeout(tcs.Task, timeout);
    }

    /// <summary>
    /// このアプリはKestrelのバックグラウンドプロセスで可視ウィンドウを持たないため、オーナー無しで
    /// ShowDialog()を呼ぶとダイアログが前面に来ず、ブラウザ等の裏に隠れて操作できなくなることがある。
    /// TopMostな透明・最小サイズの捨てフォームをオーナーにしたうえで、ForceForegroundで確実に前面化する。
    /// </summary>
    private static Form CreateTopMostOwner()
    {
        var owner = new Form
        {
            TopMost = true,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-2000, -2000),
            Size = new System.Drawing.Size(1, 1),
            Opacity = 0
        };
        owner.Show();
        ForceForeground(owner.Handle);
        return owner;
    }

    /// <summary>
    /// SetForegroundWindowは、呼び出し元スレッドが現在のフォアグラウンドスレッドと同じ入力キューを
    /// 共有していないと、Windowsのフォアグラウンドロックにより黙って失敗することがある
    /// (バックグラウンドプロセスから呼んだ場合に典型的に起きる)。
    /// AttachThreadInputで一時的に入力キューを結合してから呼び出すことで、これを回避する。
    /// </summary>
    private static void ForceForeground(IntPtr hWnd)
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        uint foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
        uint currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(foregroundThreadId, currentThreadId, true);
            try
            {
                SetForegroundWindow(hWnd);
            }
            finally
            {
                AttachThreadInput(foregroundThreadId, currentThreadId, false);
            }
        }
        else
        {
            SetForegroundWindow(hWnd);
        }
    }

    private static async Task<string?> WithTimeout(Task<string?> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException("フォルダ選択がタイムアウトしました。");
        return await task;
    }
}
