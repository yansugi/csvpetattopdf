const ctxRaw = sessionStorage.getItem('doneContext');
if (!ctxRaw) {
  window.location.href = 'index.html';
}
const ctx = JSON.parse(ctxRaw);

document.getElementById('doneTitle').textContent = `${ctx.processed}件のPDFを生成しました`;
document.getElementById('doneSub').textContent = `${ctx.outputFolderPath} に保存されました`;

document.getElementById('openFolderButton').addEventListener('click', async () => {
  try {
    await fetch('/api/dialogs/open-folder', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path: ctx.outputFolderPath })
    });
  } catch {
    // フォルダを開く操作の失敗は致命的ではないため、ここでは静かに無視する。
  }
});
