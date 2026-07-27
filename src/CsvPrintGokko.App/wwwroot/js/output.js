const ctxRaw = sessionStorage.getItem('outputContext');
if (!ctxRaw) {
  window.location.href = 'index.html';
}
const ctx = JSON.parse(ctxRaw);

const errorEl = document.getElementById('error');
const summaryEl = document.getElementById('summary');
const modeCombined = document.getElementById('modeCombined');
const modeIndividual = document.getElementById('modeIndividual');
const modeCombinedLabel = document.getElementById('modeCombinedLabel');
const modeIndividualLabel = document.getElementById('modeIndividualLabel');
const filenamePatternRow = document.getElementById('filenamePatternRow');
const filenamePatternInput = document.getElementById('filenamePatternInput');
const outputFolderInput = document.getElementById('outputFolderInput');
const browseFolderButton = document.getElementById('browseFolderButton');
const progressRow = document.getElementById('progressRow');
const progressFill = document.getElementById('progressFill');
const progressText = document.getElementById('progressText');
const runButton = document.getElementById('runButton');
const backToEditorLink = document.getElementById('backToEditorLink');

backToEditorLink.href = `editor.html?templateId=${encodeURIComponent(ctx.templateId)}`;
summaryEl.textContent = `${ctx.rowCount}行分のPDFを出力します`;

function showError(message) {
  errorEl.textContent = message;
  errorEl.classList.remove('hidden');
}

function selectMode(mode) {
  modeCombined.checked = mode === 'combined';
  modeIndividual.checked = mode === 'individual';
  modeCombinedLabel.classList.toggle('is-active', mode === 'combined');
  modeIndividualLabel.classList.toggle('is-active', mode === 'individual');
  filenamePatternRow.classList.toggle('hidden', mode !== 'individual');
}

selectMode(ctx.outputSettings?.mode ?? 'combined');
filenamePatternInput.value = ctx.outputSettings?.filenamePattern || '{列1}.pdf';

modeCombinedLabel.addEventListener('click', () => selectMode('combined'));
modeIndividualLabel.addEventListener('click', () => selectMode('individual'));

browseFolderButton.addEventListener('click', async () => {
  browseFolderButton.disabled = true;
  try {
    const res = await fetch('/api/dialogs/browse-folder', { method: 'POST' });
    if (!res.ok) throw new Error(await res.text());
    const data = await res.json();
    if (data.path) outputFolderInput.value = data.path;
  } catch (err) {
    showError(`フォルダ選択に失敗しました: ${err.message}`);
  } finally {
    browseFolderButton.disabled = false;
  }
});

runButton.addEventListener('click', async () => {
  const mode = modeCombined.checked ? 'combined' : 'individual';
  const folder = outputFolderInput.value.trim();
  if (!folder) {
    showError('保存先フォルダを選択してください。');
    return;
  }

  runButton.disabled = true;
  progressRow.classList.remove('hidden');
  progressFill.style.width = '0%';
  progressText.textContent = `0 / ${ctx.rowCount} 件 生成中...`;

  try {
    const startRes = await fetch('/api/output/start', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        templateId: ctx.templateId,
        fields: ctx.fields,
        csvSessionId: ctx.csvSessionId,
        mode,
        filenamePattern: filenamePatternInput.value || '{列1}.pdf',
        outputFolderPath: folder
      })
    });
    if (!startRes.ok) throw new Error(await startRes.text());
    const { jobId } = await startRes.json();

    await pollUntilDone(jobId, folder);
  } catch (err) {
    showError(`出力に失敗しました: ${err.message}`);
    runButton.disabled = false;
  }
});

async function pollUntilDone(jobId, folder) {
  for (;;) {
    const res = await fetch(`/api/output/${jobId}/status`);
    const status = await res.json();
    const pct = status.total === 0 ? 0 : Math.round((status.processed / status.total) * 100);
    progressFill.style.width = `${pct}%`;
    progressText.textContent = `${status.processed} / ${status.total} 件 生成中...`;

    if (status.state === 'completed') {
      sessionStorage.setItem('doneContext', JSON.stringify({ processed: status.processed, outputFolderPath: folder }));
      window.location.href = 'done.html';
      return;
    }
    if (status.state === 'failed') {
      throw new Error(status.errorMessage || '不明なエラーが発生しました。');
    }
    await new Promise((resolve) => setTimeout(resolve, 400));
  }
}
