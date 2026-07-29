// 画面を開いている間、定期的にサーバーへ生存通知(ハートビート)を送る。
// サーバー側はこれを見て、ブラウザが全て閉じられたらバックグラウンドプロセスを自動終了する。
const HEARTBEAT_INTERVAL_MS = 10000;
const clientId = crypto.randomUUID();

function sendHeartbeat() {
  fetch('/api/heartbeat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ clientId }),
    keepalive: true
  }).catch(() => {
    // サーバーが既に終了している等、生存通知に失敗しても画面側は無視してよい。
  });
}

sendHeartbeat();
setInterval(sendHeartbeat, HEARTBEAT_INTERVAL_MS);

// タブを閉じる・別ページへ離脱する瞬間に即座に切断を通知する(このリクエストは
// fetchでは間に合わないことがあるためsendBeaconを使う)。
window.addEventListener('pagehide', () => {
  const payload = new Blob([JSON.stringify({ clientId })], { type: 'application/json' });
  navigator.sendBeacon('/api/heartbeat/bye', payload);
});
