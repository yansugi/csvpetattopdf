using System.Collections.Concurrent;

namespace CsvPrintGokko.App.Services;

/// <summary>
/// ブラウザ側から送られるハートビートを元に、接続中のタブ(クライアント)を管理する。
/// 一度もブラウザ接続が無い状態(curl等での動作確認)では自動終了させないよう、
/// 「これまでに一度でも接続があったか」を別途フラグで保持する。
/// </summary>
public sealed class HeartbeatTracker
{
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeenUtcByClient = new();
    private volatile bool _hasEverConnected;

    /// <summary>クライアントからのハートビートを記録する。</summary>
    public void Touch(Guid clientId)
    {
        _hasEverConnected = true;
        _lastSeenUtcByClient[clientId] = DateTime.UtcNow;
    }

    /// <summary>タブが閉じられたときの明示的な切断通知を記録する。</summary>
    public void Disconnect(Guid clientId)
    {
        _lastSeenUtcByClient.TryRemove(clientId, out _);
    }

    /// <summary>
    /// 一定時間ハートビートが無い古いクライアントを掃除したうえで、
    /// 「これまでに接続があった」かつ「現在は誰も接続していない」かどうかを返す。
    /// </summary>
    public bool ShouldShutDown(TimeSpan idleTimeout)
    {
        DateTime cutoffUtc = DateTime.UtcNow - idleTimeout;
        foreach (var (clientId, lastSeenUtc) in _lastSeenUtcByClient)
        {
            if (lastSeenUtc < cutoffUtc)
                _lastSeenUtcByClient.TryRemove(clientId, out _);
        }

        return _hasEverConnected && _lastSeenUtcByClient.IsEmpty;
    }
}
