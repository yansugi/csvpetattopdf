import { listTemplates, createTemplate } from './api-client.js';

const errorEl = document.getElementById('error');
const listEl = document.getElementById('templateList');
const emptyHintEl = document.getElementById('emptyHint');
const createButton = document.getElementById('createButton');
const pdfFileInput = document.getElementById('pdfFileInput');
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
      const row = document.createElement('div');
      row.className = 'home-row';
      row.innerHTML = `
        <span class="fname">${escapeHtml(t.templateName)}</span>
        <span class="fdate">${formatDate(t.updatedAtUtc)} 更新</span>
        <a class="btn" href="editor.html?templateId=${encodeURIComponent(t.templateId)}">開く</a>
      `;
      listEl.appendChild(row);
    }
  } catch (err) {
    showError(`テンプレート一覧の取得に失敗しました: ${err.message}`);
  }
}

createButton.addEventListener('click', () => {
  clearError();
  pdfFileInput.value = '';
  pdfFileInput.click();
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

nameConfirmButton.addEventListener('click', async () => {
  const name = templateNameInput.value.trim();
  if (!name) {
    showError('テンプレート名を入力してください。');
    return;
  }
  if (!pendingPdfFile) return;

  nameConfirmButton.disabled = true;
  try {
    const layout = await createTemplate(name, pendingPdfFile);
    window.location.href = `editor.html?templateId=${encodeURIComponent(layout.templateId)}`;
  } catch (err) {
    showError(`テンプレートの作成に失敗しました: ${err.message}`);
    nameConfirmButton.disabled = false;
  }
});

loadTemplateList();
