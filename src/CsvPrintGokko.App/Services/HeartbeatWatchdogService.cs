namespace CsvPrintGokko.App.Services;

/// <summary>
/// ブラウザの全タブが閉じられた(ハートビートが途絶えた)ことを定期的に検知し、
/// アプリを自動終了させるバックグラウンドサービス。ユーザーがブラウザだけ閉じて
/// 裏でプロセスが残り続ける問題への対処。
/// </summary>
public sealed class HeartbeatWatchdogService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    // ブラウザのバックグラウンドタブはタイマーが間引かれることがあるため、
    // 誤ってシャットダウンしないよう余裕を持たせた閾値にする。
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(90);

    private readonly HeartbeatTracker _tracker;
    private readonly IHostApplicationLifetime _lifetime;

    public HeartbeatWatchdogService(HeartbeatTracker tracker, IHostApplicationLifetime lifetime)
    {
        _tracker = tracker;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (_tracker.ShouldShutDown(IdleTimeout))
            {
                Console.WriteLine("ブラウザとの接続が途絶えたため、アプリを終了します。");
                _lifetime.StopApplication();
                return;
            }
        }
    }
}
