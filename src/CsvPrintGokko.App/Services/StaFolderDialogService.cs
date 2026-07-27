namespace CsvPrintGokko.App.Services;

/// <summary>
/// System.Windows.Forms.FolderBrowserDialog/OpenFileDialogを呼び出すサービス。
/// Phase 0のスパイク検証で判明した通り、非STAスレッド(Kestrelのリクエストハンドラスレッド等)から
/// ShowDialog()を呼んでも例外にならず、ユーザーに見えない/操作できないダイアログのまま
/// 無期限にハングし得る。そのため必ず専用のSTAスレッドを起動して呼び出し、
/// 呼び出し側にもタイムアウトを設けることで、誤操作時にリクエストが永久に固まるのを防ぐ。
/// </summary>
public sealed class StaFolderDialogService
{
    public Task<string?> BrowseFolderAsync(string description, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<string?>();

        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = description,
                    ShowNewFolderButton = true
                };
                var result = dialog.ShowDialog();
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
                using var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter,
                    CheckFileExists = true,
                    Multiselect = false
                };
                var result = dialog.ShowDialog();
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

    private static async Task<string?> WithTimeout(Task<string?> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException("フォルダ選択がタイムアウトしました。");
        return await task;
    }
}
