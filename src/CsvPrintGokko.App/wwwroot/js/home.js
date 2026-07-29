import { listTemplates, createTemplate, deleteTemplate, importProject } from './api-client.js';

const errorEl = document.getElementById('error');
const listEl = document.getElementById('templateList');
const emptyHintEl = document.getElementById('emptyHint');
const createButton = document.getElementById('createButton');
const pdfFileInput = document.getElementById('pdfFileInput');
const importButton = document.getElementById('importButton');
const importFileInput = document.getElementById('importFileInput');
const nameModal = document.getElementById('nameModal');
const templateNameInput = document.getElementById('templateNameInput');
const nameCancelButton = document.getElementById('nameCancelButton');
const nameConfirmButton = document.getElementById('nameConfirmButton');

let pendingPdfFile = null;

function showError(message) {
  errorEl.textContent = message;
  errorEl.classList.remove('hidden');
}

function clearError() {
  errorEl.classList.add('hidden');
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function formatDate(isoString) {
  const date = new Date(isoString);
  return date.toLocaleString('ja-JP', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
}

async function loadTemplateList() {
  try {
    const templates = await listTemplates();
    listEl.innerHTML = '';
    if (templates.length === 0) {
      emptyHintEl.classList.remove('hidden');
      return;
    }
    emptyHintEl.classList.add('hidden');
    for (const t of templates) {
      const isList = t.kind === 'list';
      const row = document.createElement('div');
      row.className = 'home-row';
      row.innerHTML = `
        <span class="fname-wrap">
          <span class="fname">${escapeHtml(t.templateName)}</span>
          <span class="kind-badge ${isList ? 'kind-list' : 'kind-single'}">${isList ? '一覧表' : '単票'}</span>
        </span>
        <span class="fdate">${formatDate(t.updatedAtUtc)} 更新</span>
        <a class="btn" href="editor.html?templateId=${encodeURIComponent(t.templateId)}">開く</a>
        <button class="btn btn-danger" type="button" data-delete-id="${t.templateId}" data-delete-name="${escapeHtml(t.templateName)}">削除</button>
      `;
      listEl.appendChild(row);
    }
  } catch (err) {
    showError(`テンプレート一覧の取得に失敗しました: ${err.message}`);
  }
}

// 削除ボタンは一覧を再描画するたびに行ごと作り直されるため、リスト全体への
// イベント委譲で拾う(個別の行に都度リスナーを付け外しする必要をなくすため)。
listEl.addEventListener('click', async (event) => {
  const button = event.target.closest('button[data-delete-id]');
  if (!button) return;

  const templateId = button.dataset.deleteId;
  const templateName = button.dataset.deleteName;
  if (!window.confirm(`テンプレート「${templateName}」を削除します。この操作は元に戻せません。よろしいですか?`)) {
    return;
  }

  clearError();
  button.disabled = true;
  try {
    await deleteTemplate(templateId);
    await loadTemplateList();
  } catch (err) {
    showError(`テンプレートの削除に失敗しました: ${err.message}`);
    button.disabled = false;
  }
});

createButton.addEventListener('click', () => {
  clearError();
  pdfFileInput.value = '';
  pdfFileInput.click();
});

importButton.addEventListener('click', () => {
  clearError();
  importFileInput.value = '';
  importFileInput.click();
});

importFileInput.addEventListener('change', async () => {
  if (importFileInput.files.length === 0) return;
  const file = importFileInput.files[0];

  importButton.disabled = true;
  try {
    const layout = await importProject(file);
    window.location.href = `editor.html?templateId=${encodeURIComponent(layout.templateId)}`;
  } catch (err) {
    showError(`プロジェクトファイルの読み込みに失敗しました: ${err.message}`);
    importButton.disabled = false;
  }
});

pdfFileInput.addEventListener('change', () => {
  if (pdfFileInput.files.length === 0) return;
  pendingPdfFile = pdfFileInput.files[0];
  templateNameInput.value = pendingPdfFile.name.replace(/\.pdf$/i, '');
  nameModal.classList.remove('hidden');
  templateNameInput.focus();
});

nameCancelButton.addEventListener('click', () => {
  nameModal.classList.add('hidden');
  pendingPdfFile = null;
});

const kindSingleLabel = document.getElementById('kindSingleLabel');
const kindListLabel = document.getElementById('kindListLabel');
function updateKindHighlight() {
  const kind = document.querySelector('input[name="kind"]:checked').value;
  kindSingleLabel.classList.toggle('is-active', kind === 'single');
  kindListLabel.classList.toggle('is-active', kind === 'list');
}
kindSingleLabel.addEventListener('click', () => setTimeout(updateKindHighlight));
kindListLabel.addEventListener('click', () => setTimeout(updateKindHighlight));
updateKindHighlight();

nameConfirmButton.addEventListener('click', async () => {
  const name = templateNameInput.value.trim();
  if (!name) {
    showError('テンプレート名を入力してください。');
    return;
  }
  if (!pendingPdfFile) return;
  const kind = document.querySelector('input[name="kind"]:checked').value;

  nameConfirmButton.disabled = true;
  try {
    const layout = await createTemplate(name, pendingPdfFile, kind);
    window.location.href = `editor.html?templateId=${encodeURIComponent(layout.templateId)}`;
  } catch (err) {
    showError(`テンプレートの作成に失敗しました: ${err.message}`);
    nameConfirmButton.disabled = false;
  }
});

loadTemplateList();
