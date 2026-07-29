using CsvPrintGokko.App.Services;

namespace CsvPrintGokko.App.Endpoints;

/// <summary>ブラウザタブの生存確認(ハートビート)を受け取るエンドポイント群。</summary>
public static class HeartbeatEndpoints
{
    public static void MapHeartbeatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/heartbeat");

        group.MapPost("/", (HeartbeatRequest request, HeartbeatTracker tracker) =>
        {
            tracker.Touch(request.ClientId);
            return Results.NoContent();
        });

        // タブを閉じる瞬間にnavigator.sendBeaconから送られる明示的な切断通知。
        group.MapPost("/bye", (HeartbeatRequest request, HeartbeatTracker tracker) =>
        {
            tracker.Disconnect(request.ClientId);
            return Results.NoContent();
        });
    }
}

public sealed record HeartbeatRequest(Guid ClientId);
