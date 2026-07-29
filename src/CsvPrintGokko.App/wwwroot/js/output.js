import { getCsvHeaders, exportProjectUrl } from './api-client.js';

const ctxRaw = sessionStorage.getItem('outputContext');
if (!ctxRaw) {
  window.location.href = 'index.html';
}
const ctx = JSON.parse(ctxRaw);
const isList = ctx.kind === 'list';

const errorEl = document.getElementById('error');
const summaryEl = document.getElementById('summary');
const modeSection = document.getElementById('modeSection');
const listInfoHint = document.getElementById('listInfoHint');
const modeCombined = document.getElementById('modeCombined');
const modeIndividual = document.getElementById('modeIndividual');
const modeCombinedLabel = document.getElementById('modeCombinedLabel');
const modeIndividualLabel = document.getElementById('modeIndividualLabel');
const filenamePatternRow = document.getElementById('filenamePatternRow');
const filenamePatternInput = document.getElementById('filenamePatternInput');
const filenameVariableRow = document.getElementById('filenameVariableRow');
const outputFolderInput = document.getElementById('outputFolderInput');
const browseFolderButton = document.getElementById('browseFolderButton');
const progressRow = document.getElementById('progressRow');
const progressFill = document.getElementById('progressFill');
const progressText = document.getElementById('progressText');
const runButton = document.getElementById('runButton');
const backToEditorLink = document.getElementById('backToEditorLink');
const exportProjectLink = document.getElementById('exportProjectLink');

backToEditorLink.href = `editor.html?templateId=${encodeURIComponent(ctx.templateId)}`;
exportProjectLink.href = exportProjectUrl(ctx.templateId);
summaryEl.textContent = isList ? `${ctx.rowCount}行分の一覧表を出力します` : `${ctx.rowCount}行分のPDFを出力します`;

function showError(message) {
  errorEl.textContent = message;
  errorEl.classList.remove('hidden');
}

// 一覧表テンプレートは常に「CSV全行を1つの一覧表」として出力するため、結合/個別の選択自体が意味を持たない。
// モード選択UIを隠し、代わりに説明文を表示する。
if (isList) {
  modeSection.classList.add('hidden');
  listInfoHint.classList.remove('hidden');
  filenamePatternRow.classList.add('hidden');
} else {
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

  renderFilenameVariableButtons();
}

/// <summary>
/// ファイル名パターン欄の下に、クリックでカーソル位置へ"{トークン}"を挿入できるボタン列を表示する
/// (配置エディタの自由テキスト欄と同じ操作感にするため)。CSVの列名 + 行番号/出力時間を候補にする。
/// CSVの列名はsessionStorage経由(ctx.csvHeaders)ではなく、csvSessionIdからサーバーへ都度問い合わせて
/// 取得する。配置エディタのタブがリロードされておらず古いsessionStorageの形のままでも、
/// 常に実際のCSVと一致した列名一覧を表示できるようにするため。
/// </summary>
async function renderFilenameVariableButtons() {
  let csvHeaders = ctx.csvHeaders ?? [];
  try {
    const result = await getCsvHeaders(ctx.csvSessionId);
    csvHeaders = result.headers;
  } catch {
    // CSVセッションが切れている等で取得できない場合は、sessionStorageに含まれていた値(無ければ空)にフォールバックする。
  }

  const tokens = ['行番号', '出力時間', ...csvHeaders];
  if (tokens.length === 0) return;

  filenameVariableRow.innerHTML = `
    <div class="variable-insert-row">
      <span class="field-label">変数を挿入</span>
      <div class="variable-chip-list">
        ${tokens.map((t) => `<button type="button" class="variable-chip-btn" data-token="${escapeHtml(t)}">${escapeHtml(t)}</button>`).join('')}
      </div>
    </div>`;

  filenameVariableRow.querySelectorAll('.variable-chip-btn').forEach((btn) => {
    btn.addEventListener('click', () => {
      const insertText = `{${btn.dataset.token}}`;
      const start = filenamePatternInput.selectionStart ?? filenamePatternInput.value.length;
      const end = filenamePatternInput.selectionEnd ?? filenamePatternInput.value.length;
      filenamePatternInput.value = filenamePatternInput.value.slice(0, start) + insertText + filenamePatternInput.value.slice(end);
      filenamePatternInput.focus();
      filenamePatternInput.selectionStart = filenamePatternInput.selectionEnd = start + insertText.length;
    });
  });
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

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
  const mode = isList ? 'combined' : (modeCombined.checked ? 'combined' : 'individual');
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
        outputFolderPath: folder,
        listSettings: ctx.listSettings
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
