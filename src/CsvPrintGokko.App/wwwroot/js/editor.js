import { getTemplate, saveLayout, templatePdfUrl, browseCsvFile, loadCsvFromPath, renderPreview } from './api-client.js';
import { renderPdfToCanvas, pxToPt, ptToPx } from './pdf-canvas.js';

// Windows環境に標準で入っていることが多いフォントの固定リスト(動的列挙は初回取得が重いため採用しない)。
const COMMON_FONTS = [
  'Yu Gothic',
  'Yu Gothic UI',
  'Yu Mincho',
  'Meiryo',
  'MS Gothic',
  'MS Mincho',
  'BIZ UDGothic',
  'Arial',
  'Times New Roman',
  'Segoe UI'
];

const DEFAULT_FIELD_WIDTH_PT = 100;
const MIN_FIELD_WIDTH_PT = 20;
const DEFAULT_FIELD_HEIGHT_PT = 24;
const MIN_FIELD_HEIGHT_PT = 10;
const MIN_CHIP_FONT_PX = 8;
const MIN_ZOOM = 0.4;
const MAX_ZOOM = 3.0;
const ZOOM_STEP = 0.1;

/// <summary>幅・高さは小数点第2位までに丸める。</summary>
function roundTo2(value) {
  return Math.round(value * 100) / 100;
}

const params = new URLSearchParams(window.location.search);
const templateId = params.get('templateId');
if (!templateId) {
  window.location.href = 'index.html';
}

// --- 状態 ---
let layout = null;           // サーバーから取得したTemplateLayout。fieldsを直接編集する。
let displayScale = 1.3;
let selectedFieldId = null;
let csvSessionId = null;
let csvHeaders = [];
let csvRowCount = 0;
let currentRowIndex = 0;
let isPreviewMode = false;
let isPanMode = false;
let selectedCsvPath = null;
let history = [];
let historyIndex = -1;

// --- DOM参照 ---
const templateNameLabel = document.getElementById('templateNameLabel');
const paletteEl = document.getElementById('palette');
const paletteEmptyHint = document.getElementById('paletteEmptyHint');
const pdfStage = document.getElementById('pdfStage');
const pdfCanvas = document.getElementById('pdfCanvas');
const propsPanel = document.getElementById('propsPanel');
const loadCsvButton = document.getElementById('loadCsvButton');
const saveLayoutButton = document.getElementById('saveLayoutButton');
const undoButton = document.getElementById('undoButton');
const redoButton = document.getElementById('redoButton');
const panToolButton = document.getElementById('panToolButton');
const togglePreviewButton = document.getElementById('togglePreviewButton');
const goToOutputButton = document.getElementById('goToOutputButton');
const prevRowButton = document.getElementById('prevRowButton');
const nextRowButton = document.getElementById('nextRowButton');
const rowbarText = document.getElementById('rowbarText');
const errorEl = document.getElementById('error');

const csvModal = document.getElementById('csvModal');
const csvPathDisplay = document.getElementById('csvPathDisplay');
const csvBrowseButton = document.getElementById('csvBrowseButton');
const csvEncodingSelect = document.getElementById('csvEncodingSelect');
const csvDelimiterInput = document.getElementById('csvDelimiterInput');
const csvHasHeaderCheckbox = document.getElementById('csvHasHeaderCheckbox');
const csvCancelButton = document.getElementById('csvCancelButton');
const csvConfirmButton = document.getElementById('csvConfirmButton');
const canvasArea = document.getElementById('canvasArea');
const zoomInButton = document.getElementById('zoomInButton');
const zoomOutButton = document.getElementById('zoomOutButton');
const zoomLabel = document.getElementById('zoomLabel');
const staticTextChip = document.getElementById('staticTextChip');
const calcChip = document.getElementById('calcChip');

staticTextChip.addEventListener('dragstart', (e) => {
  e.dataTransfer.setData('text/field-kind', 'text');
});

calcChip.addEventListener('dragstart', (e) => {
  e.dataTransfer.setData('text/field-kind', 'calc');
});

function showError(message) {
  errorEl.textContent = message;
  errorEl.classList.remove('hidden');
  clearTimeout(showError._timer);
  showError._timer = setTimeout(() => errorEl.classList.add('hidden'), 6000);
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text ?? '';
  return div.innerHTML;
}

// --- Undo/Redo(フィールドの配置・サイズ・プロパティの変更履歴) ---
function cloneFields() {
  return JSON.parse(JSON.stringify(layout.fields));
}

function resetHistory() {
  history = [cloneFields()];
  historyIndex = 0;
  updateUndoRedoButtons();
}

/// <summary>一連の操作(ドラッグ・リサイズ・入力確定・追加・削除)の完了時点のスナップショットを積む。</summary>
function commitHistory() {
  history = history.slice(0, historyIndex + 1);
  history.push(cloneFields());
  historyIndex = history.length - 1;
  updateUndoRedoButtons();
}

function restoreHistoryAt(index) {
  historyIndex = index;
  layout.fields = JSON.parse(JSON.stringify(history[historyIndex]));
  if (!layout.fields.some((f) => f.id === selectedFieldId)) {
    selectedFieldId = null;
  }
  renderFieldChips();
  renderPropsPanel();
  updateUndoRedoButtons();
}

function undo() {
  if (historyIndex <= 0) return;
  restoreHistoryAt(historyIndex - 1);
}

function redo() {
  if (historyIndex >= history.length - 1) return;
  restoreHistoryAt(historyIndex + 1);
}

function updateUndoRedoButtons() {
  undoButton.disabled = historyIndex <= 0;
  redoButton.disabled = historyIndex >= history.length - 1;
}

undoButton.addEventListener('click', undo);
redoButton.addEventListener('click', redo);

function deleteField(fieldId) {
  layout.fields = layout.fields.filter((f) => f.id !== fieldId);
  selectedFieldId = null;
  renderFieldChips();
  renderPropsPanel();
  commitHistory();
}

// キーボードショートカット: Ctrl+Z(元に戻す)/Ctrl+Y・Ctrl+Shift+Z(やり直す)/Delete・Backspace(選択フィールド削除)。
// フォーム入力中はブラウザ標準のUndo/編集操作を優先し、ショートカットを奪わない。
document.addEventListener('keydown', (e) => {
  const tag = e.target.tagName;
  if (tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA') return;

  const key = e.key.toLowerCase();
  if (e.ctrlKey && !e.shiftKey && key === 'z') {
    e.preventDefault();
    undo();
  } else if (e.ctrlKey && (key === 'y' || (e.shiftKey && key === 'z'))) {
    e.preventDefault();
    redo();
  } else if ((e.key === 'Delete' || e.key === 'Backspace') && selectedFieldId) {
    e.preventDefault();
    deleteField(selectedFieldId);
  }
});

// --- 手のひらツール(拡大時にドラッグでキャンバスを自由に移動) ---
panToolButton.addEventListener('click', () => {
  isPanMode = !isPanMode;
  panToolButton.classList.toggle('is-active', isPanMode);
  canvasArea.classList.toggle('pan-mode', isPanMode);
});

canvasArea.addEventListener('mousedown', (e) => {
  if (!isPanMode) return;
  e.preventDefault();

  const startX = e.clientX;
  const startY = e.clientY;
  const startScrollLeft = canvasArea.scrollLeft;
  const startScrollTop = canvasArea.scrollTop;
  canvasArea.classList.add('panning');

  function onMouseMove(moveEvent) {
    canvasArea.scrollLeft = startScrollLeft - (moveEvent.clientX - startX);
    canvasArea.scrollTop = startScrollTop - (moveEvent.clientY - startY);
  }

  function onMouseUp() {
    canvasArea.classList.remove('panning');
    document.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('mouseup', onMouseUp);
  }

  document.addEventListener('mousemove', onMouseMove);
  document.addEventListener('mouseup', onMouseUp);
});

// --- 初期化 ---
async function init() {
  try {
    layout = await getTemplate(templateId);
  } catch (err) {
    showError(`テンプレートの読み込みに失敗しました: ${err.message}`);
    return;
  }

  // 旧レイアウト等で未設定(null/undefined)のフィールドにも、既定値を補う。
  for (const field of layout.fields) {
    if (field.maxWidthPt == null) field.maxWidthPt = DEFAULT_FIELD_WIDTH_PT;
    if (field.heightPt == null) field.heightPt = DEFAULT_FIELD_HEIGHT_PT;
    if (field.verticalAlign == null) field.verticalAlign = 'top';
    if (field.kind == null) field.kind = 'csv';
    if (field.locked == null) field.locked = false;
    if (field.dataType == null) field.dataType = 'text';
    if (field.dateFormatKind == null) field.dateFormatKind = 'slash';
    if (field.dateCustomFormat == null) field.dateCustomFormat = '';
    if (field.numberDecimalPlaces === undefined) field.numberDecimalPlaces = null;
    if (field.numberUseThousandsSeparator == null) field.numberUseThousandsSeparator = false;
    if (field.numberPrefix == null) field.numberPrefix = '';
    if (field.numberSuffix == null) field.numberSuffix = '';
    if (field.booleanTrueValues == null) field.booleanTrueValues = 'true,1,○,有,済';
    if (field.booleanTrueDisplay == null) field.booleanTrueDisplay = '✓';
    if (field.booleanFalseDisplay == null) field.booleanFalseDisplay = '';
    if (field.formula == null) field.formula = '';
  }
  resetHistory();

  templateNameLabel.textContent = layout.templateName;
  csvEncodingSelect.value = layout.csvSettings.encoding;
  csvDelimiterInput.value = layout.csvSettings.delimiter;
  csvHasHeaderCheckbox.checked = layout.csvSettings.hasHeader;
  if (layout.csvSettings.lastFilePath) {
    selectedCsvPath = layout.csvSettings.lastFilePath;
    csvPathDisplay.value = selectedCsvPath;
  }

  await renderTemplateBackground();
  updateZoomLabel();
  renderFieldChips();
  renderPropsPanel();

  // 前回読み込んだCSVがあれば自動で再読込し、パレット・行送りの状態を復元する。
  if (layout.csvSettings.lastFilePath) {
    try {
      const result = await loadCsvFromPath(
        layout.csvSettings.lastFilePath,
        layout.csvSettings.encoding,
        layout.csvSettings.delimiter,
        layout.csvSettings.hasHeader
      );
      csvSessionId = result.csvSessionId;
      csvHeaders = result.headers;
      csvRowCount = result.rowCount;
      currentRowIndex = 0;

      renderPalette();
      renderFieldChips();
      renderPropsPanel();
      updateRowbar();
      togglePreviewButton.disabled = csvRowCount === 0;
      prevRowButton.disabled = csvRowCount === 0;
      nextRowButton.disabled = csvRowCount === 0;
    } catch (err) {
      showError(`前回のCSVファイルの自動読み込みに失敗しました。CSV読込からやり直してください。(${err.message})`);
    }
  }
}

// --- 配置キャンバスのズーム ---
function updateZoomLabel() {
  zoomLabel.textContent = `${Math.round(displayScale * 100)}%`;
}

// ズームボタン連打やホイール連続操作でpdf.jsのrender()が同じcanvasに対して重複実行されないよう、
// 呼び出しを1本の直列チェーンに繋いで前の描画完了を待ってから次を実行する。
let zoomRenderChain = Promise.resolve();

function setZoom(newScale) {
  const clamped = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, newScale));
  if (clamped === displayScale) return zoomRenderChain;
  displayScale = clamped;
  zoomRenderChain = zoomRenderChain.then(async () => {
    if (isPreviewMode) {
      await showPreview();
    } else {
      await renderTemplateBackground();
      renderFieldChips();
    }
    updateZoomLabel();
  });
  return zoomRenderChain;
}

zoomInButton.addEventListener('click', () => setZoom(displayScale + ZOOM_STEP));
zoomOutButton.addEventListener('click', () => setZoom(displayScale - ZOOM_STEP));

// Ctrl+マウスホイールでも配置キャンバスだけを拡大縮小できるようにする(ブラウザ全体のページズームは抑止)。
canvasArea.addEventListener('wheel', (e) => {
  if (!e.ctrlKey) return;
  e.preventDefault();
  setZoom(displayScale + (e.deltaY < 0 ? ZOOM_STEP : -ZOOM_STEP));
}, { passive: false });

async function renderTemplateBackground() {
  const res = await fetch(templatePdfUrl(templateId));
  const buffer = await res.arrayBuffer();
  const info = await renderPdfToCanvas(buffer, pdfCanvas, displayScale);
  displayScale = info.displayScale;
  pdfStage.style.width = `${pdfCanvas.width}px`;
  pdfStage.style.height = `${pdfCanvas.height}px`;
}

// --- CSV列パレット ---
function renderPalette() {
  paletteEl.querySelectorAll('.csv-chip').forEach((el) => el.remove());
  if (csvHeaders.length === 0) {
    paletteEmptyHint.classList.remove('hidden');
    return;
  }
  paletteEmptyHint.classList.add('hidden');
  for (const header of csvHeaders) {
    const chip = document.createElement('div');
    chip.className = 'csv-chip';
    chip.draggable = true;
    chip.innerHTML = `<span class="grip">⋮⋮</span> ${escapeHtml(header)}`;
    chip.addEventListener('dragstart', (e) => {
      e.dataTransfer.setData('text/csv-column', header);
    });
    paletteEl.appendChild(chip);
  }
}

pdfStage.addEventListener('dragover', (e) => e.preventDefault());

pdfStage.addEventListener('drop', (e) => {
  e.preventDefault();
  if (isPreviewMode) return;
  const fieldKind = e.dataTransfer.getData('text/field-kind');
  const column = e.dataTransfer.getData('text/csv-column');
  const isToolField = fieldKind === 'text' || fieldKind === 'calc';
  if (!isToolField && !column) return;

  const stageRect = pdfStage.getBoundingClientRect();
  const xPx = e.clientX - stageRect.left;
  const yPx = e.clientY - stageRect.top;

  const newField = {
    id: crypto.randomUUID(),
    kind: isToolField ? fieldKind : 'csv',
    csvColumn: isToolField ? null : column,
    staticText: fieldKind === 'text' ? 'テキスト' : null,
    formula: '',
    label: null,
    x: pxToPt(xPx, displayScale),
    y: pxToPt(yPx, displayScale),
    fontFamily: 'Yu Gothic',
    fontSizePt: 12,
    color: '#1F2A2E',
    backgroundColor: null,
    align: 'left',
    verticalAlign: 'top',
    overflow: 'none',
    maxWidthPt: DEFAULT_FIELD_WIDTH_PT,
    heightPt: DEFAULT_FIELD_HEIGHT_PT,
    locked: false,
    dataType: 'text',
    dateFormatKind: 'slash',
    dateCustomFormat: '',
    numberDecimalPlaces: null,
    numberUseThousandsSeparator: false,
    numberPrefix: '',
    numberSuffix: '',
    booleanTrueValues: 'true,1,○,有,済',
    booleanTrueDisplay: '✓',
    booleanFalseDisplay: ''
  };
  layout.fields.push(newField);
  selectField(newField.id);
  commitHistory();
});

// --- 配置キャンバス上のフィールドチップ ---
function renderFieldChips() {
  pdfStage.querySelectorAll('.field-chip').forEach((el) => el.remove());

  for (const field of layout.fields) {
    const chip = document.createElement('div');
    // 外側の.field-chipはリサイズハンドルの表示領域を確保するため常にoverflow:visibleとし、
    // テキストのはみ出し処理は内側の.field-chip-contentだけに適用する。
    // overflow-${...}クラスは枠線の見た目(そのままの場合は点線)だけに使う。
    chip.className = `field-chip overflow-${field.overflow}${field.locked ? ' locked' : ''}`;
    chip.style.left = `${ptToPx(field.x, displayScale)}px`;
    chip.style.top = `${ptToPx(field.y, displayScale)}px`;
    chip.style.width = `${ptToPx(field.maxWidthPt, displayScale)}px`;
    chip.style.height = `${ptToPx(field.heightPt, displayScale)}px`;
    // 背景色未設定時は空文字にしてCSSクラス側の既定色(選択中/未選択の色分け)に戻す。
    chip.style.backgroundColor = field.backgroundColor || '';

    const content = document.createElement('div');
    content.className = `field-chip-content overflow-${field.overflow}`;
    if (field.kind === 'text') {
      content.textContent = field.staticText || '(空のテキスト)';
    } else if (field.kind === 'calc') {
      content.textContent = field.formula ? `= ${field.formula}` : '(計算式未設定)';
    } else {
      content.textContent = field.label || field.csvColumn;
    }
    content.style.fontSize = `${Math.max(field.fontSizePt * displayScale * 0.9, 10)}px`;
    content.style.color = field.color;
    content.style.textAlign = field.align;
    content.style.justifyContent = { top: 'flex-start', middle: 'center', bottom: 'flex-end' }[field.verticalAlign] ?? 'flex-start';

    // 「はみ出し時」設定を編集画面上でもその場で見た目に反映する(実際のPDF合成ロジックの近似)。
    // white-space: pre*系にして、自由テキストの改行(\n)がそのまま複数行として表示されるようにする。
    switch (field.overflow) {
      case 'wrap':
        content.style.whiteSpace = 'pre-wrap';
        content.style.overflowWrap = 'anywhere';
        content.style.overflow = 'hidden';
        break;
      case 'clip':
        content.style.whiteSpace = 'pre';
        content.style.overflow = 'hidden';
        break;
      case 'shrink':
        content.style.whiteSpace = 'pre';
        content.style.overflow = 'hidden';
        break;
      default: // none(ボックスはあくまで目安なので、はみ出しても見た目には反映しない)
        content.style.whiteSpace = 'pre';
        content.style.overflow = 'visible';
        break;
    }

    chip.appendChild(content);

    if (field.kind === 'csv' && csvHeaders.length > 0 && !csvHeaders.includes(field.csvColumn)) {
      chip.classList.add('missing-column');
      chip.title = 'CSVにこの列が見つかりません';
    }
    if (field.id === selectedFieldId) chip.classList.add('selected');

    chip.addEventListener('mousedown', (e) => startDragField(e, field));
    chip.addEventListener('click', (e) => {
      e.stopPropagation();
      selectField(field.id);
    });

    pdfStage.appendChild(chip);

    if (field.overflow === 'shrink') {
      shrinkChipFontToFit(content);
    }

    // ロック中はリサイズハンドルを出さず、誤操作でのサイズ変更を防ぐ。
    if (field.id === selectedFieldId && !field.locked) {
      addResizeHandle(chip, field, 'field-resize-handle-e', { resizeWidth: true, resizeHeight: false });
      addResizeHandle(chip, field, 'field-resize-handle-s', { resizeWidth: false, resizeHeight: true });
      addResizeHandle(chip, field, 'field-resize-handle-se', { resizeWidth: true, resizeHeight: true });
    }
  }
}

function addResizeHandle(chip, field, className, axes) {
  const handle = document.createElement('div');
  handle.className = `field-resize-handle ${className}`;
  handle.addEventListener('mousedown', (e) => startResizeField(e, field, axes));
  chip.appendChild(handle);
}

/// <summary>幅・高さに収まるまでフォントサイズを段階的に縮小する(PdfComposerServiceのShrinkToFitの編集画面向け近似)。</summary>
function shrinkChipFontToFit(content) {
  let sizePx = parseFloat(content.style.fontSize);
  while (sizePx > MIN_CHIP_FONT_PX && (content.scrollWidth > content.clientWidth || content.scrollHeight > content.clientHeight)) {
    sizePx -= 1;
    content.style.fontSize = `${sizePx}px`;
  }
}

pdfStage.addEventListener('click', () => selectField(null));

function selectField(fieldId) {
  selectedFieldId = fieldId;
  renderFieldChips();
  renderPropsPanel();
}

function startDragField(e, field) {
  if (isPreviewMode) return;
  // ロック中のフィールドは選択のみ許可し、位置移動は行わない。
  if (field.locked) {
    e.stopPropagation();
    selectField(field.id);
    return;
  }
  e.preventDefault();
  e.stopPropagation();
  selectField(field.id);

  const stageRect = pdfStage.getBoundingClientRect();
  const startX = e.clientX;
  const startY = e.clientY;
  const originLeftPx = ptToPx(field.x, displayScale);
  const originTopPx = ptToPx(field.y, displayScale);
  let moved = false;

  function onMouseMove(moveEvent) {
    const dx = moveEvent.clientX - startX;
    const dy = moveEvent.clientY - startY;
    const newLeftPx = Math.max(0, Math.min(originLeftPx + dx, stageRect.width - 10));
    const newTopPx = Math.max(0, Math.min(originTopPx + dy, stageRect.height - 10));

    field.x = pxToPt(newLeftPx, displayScale);
    field.y = pxToPt(newTopPx, displayScale);
    moved = true;
    renderFieldChips();
    if (field.id === selectedFieldId) updatePropsPanelPosition(field);
  }

  function onMouseUp() {
    document.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('mouseup', onMouseUp);
    if (moved) commitHistory();
  }

  document.addEventListener('mousemove', onMouseMove);
  document.addEventListener('mouseup', onMouseUp);
}

function updatePropsPanelPosition(field) {
  const xInput = document.getElementById('propX');
  const yInput = document.getElementById('propY');
  if (xInput) xInput.value = field.x.toFixed(1);
  if (yInput) yInput.value = field.y.toFixed(1);
}

function startResizeField(e, field, { resizeWidth, resizeHeight }) {
  e.preventDefault();
  e.stopPropagation();

  const startX = e.clientX;
  const startY = e.clientY;
  const originWidthPx = ptToPx(field.maxWidthPt, displayScale);
  const originHeightPx = ptToPx(field.heightPt, displayScale);
  let resized = false;

  function onMouseMove(moveEvent) {
    if (resizeWidth) {
      const dx = moveEvent.clientX - startX;
      const newWidthPx = Math.max(ptToPx(MIN_FIELD_WIDTH_PT, displayScale), originWidthPx + dx);
      field.maxWidthPt = roundTo2(pxToPt(newWidthPx, displayScale));
    }
    if (resizeHeight) {
      const dy = moveEvent.clientY - startY;
      const newHeightPx = Math.max(ptToPx(MIN_FIELD_HEIGHT_PT, displayScale), originHeightPx + dy);
      field.heightPt = roundTo2(pxToPt(newHeightPx, displayScale));
    }
    resized = true;
    renderFieldChips();
    if (field.id === selectedFieldId) updatePropsPanelSize(field);
  }

  function onMouseUp() {
    document.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('mouseup', onMouseUp);
    if (resized) commitHistory();
  }

  document.addEventListener('mousemove', onMouseMove);
  document.addEventListener('mouseup', onMouseUp);
}

function updatePropsPanelSize(field) {
  const widthInput = document.getElementById('propMaxWidth');
  const heightInput = document.getElementById('propHeight');
  if (widthInput) widthInput.value = field.maxWidthPt.toFixed(2);
  if (heightInput) heightInput.value = field.heightPt.toFixed(2);
}

// --- プロパティパネル ---
function buildCsvColumnOptionsHtml(currentColumn) {
  // CSV未読込、または保存済みレイアウトの列が現在のCSVに無い場合も、
  // 選択が消えないよう先頭に補完して表示する。
  const base = csvHeaders.length > 0 ? csvHeaders : [currentColumn];
  const names = base.includes(currentColumn) ? base : [currentColumn, ...base];
  return names
    .map((name) => `<option value="${escapeHtml(name)}" ${name === currentColumn ? 'selected' : ''}>${escapeHtml(name)}</option>`)
    .join('');
}

function buildFontOptionsHtml(currentFontFamily) {
  // 保存済みレイアウトのフォントが固定リストに無い場合も、
  // 選択が消えないよう先頭に補完して表示する。
  const names = COMMON_FONTS.includes(currentFontFamily)
    ? COMMON_FONTS
    : [currentFontFamily, ...COMMON_FONTS];
  return names
    .map((name) => {
      const cssFontFamily = escapeHtml(name).replace(/'/g, "\\'");
      const selectedAttr = name === currentFontFamily ? 'selected' : '';
      return `<option value="${escapeHtml(name)}" style="font-family:'${cssFontFamily}', sans-serif;" ${selectedAttr}>${escapeHtml(name)}</option>`;
    })
    .join('');
}

/// <summary>数値の表示形式(小数桁数/桁区切り/接頭辞接尾辞)の設定行。DataType=Number・計算(Calc)フィールドの両方から使う。</summary>
function buildNumberFormatRowsHtml(field) {
  return `
    <div class="prop-row">
      <span class="field-label">小数点以下の桁数(空欄なら丸めない)</span>
      <input type="number" min="0" step="1" class="text-input mono-input" id="propNumberDecimalPlaces" value="${field.numberDecimalPlaces ?? ''}" />
    </div>
    <div class="prop-row">
      <label><input type="checkbox" id="propNumberThousands" ${field.numberUseThousandsSeparator ? 'checked' : ''} /> 3桁区切りのカンマを付ける</label>
    </div>
    <div class="prop-row">
      <span class="field-label">接頭辞(例: ¥)</span>
      <input type="text" class="text-input" id="propNumberPrefix" value="${escapeHtml(field.numberPrefix ?? '')}" />
    </div>
    <div class="prop-row">
      <span class="field-label">接尾辞(例: 円)</span>
      <input type="text" class="text-input" id="propNumberSuffix" value="${escapeHtml(field.numberSuffix ?? '')}" />
    </div>`;
}

/// <summary>データ型ドロップダウンと、選択中の型に応じた追加設定行(日付/数値/Boolean)のHTMLを組み立てる。</summary>
function buildDataTypeSettingsHtml(field) {
  let extraRowsHtml = '';
  if (field.dataType === 'date') {
    extraRowsHtml = `
    <div class="prop-row">
      <span class="field-label">日付の表示形式</span>
      <select class="select-input" id="propDateFormatKind">
        <option value="slash">yyyy/MM/dd</option>
        <option value="kanji">yyyy年MM月dd日</option>
        <option value="monthday">MM/dd</option>
        <option value="japanese">和暦(例: 令和8年7月27日)</option>
        <option value="slashwithtime">yyyy/MM/dd HH:mm</option>
        <option value="kanjiwithtime">yyyy年MM月dd日 HH時mm分</option>
        <option value="timeonly">HH:mm(時刻のみ)</option>
        <option value="japanesewithtime">和暦+時刻(例: 令和8年7月27日 14時30分)</option>
        <option value="custom">カスタム書式</option>
      </select>
    </div>
    ${field.dateFormatKind === 'custom' ? `
    <div class="prop-row">
      <span class="field-label">カスタム書式(.NET日付書式)</span>
      <input type="text" class="text-input mono-input" id="propDateCustomFormat" value="${escapeHtml(field.dateCustomFormat ?? '')}" placeholder="yyyy.MM.dd" />
    </div>` : ''}`;
  } else if (field.dataType === 'number') {
    extraRowsHtml = buildNumberFormatRowsHtml(field);
  } else if (field.dataType === 'boolean') {
    extraRowsHtml = `
    <div class="prop-row">
      <span class="field-label">「真」とみなす値(カンマ区切り)</span>
      <input type="text" class="text-input" id="propBooleanTrueValues" value="${escapeHtml(field.booleanTrueValues ?? '')}" />
    </div>
    <div class="prop-row">
      <span class="field-label">真の場合の表示</span>
      <input type="text" class="text-input" id="propBooleanTrueDisplay" value="${escapeHtml(field.booleanTrueDisplay ?? '')}" />
    </div>
    <div class="prop-row">
      <span class="field-label">偽の場合の表示</span>
      <input type="text" class="text-input" id="propBooleanFalseDisplay" value="${escapeHtml(field.booleanFalseDisplay ?? '')}" />
    </div>`;
  }

  return `
    <div class="prop-row">
      <span class="field-label">データ型</span>
      <select class="select-input" id="propDataType">
        <option value="text">文字列(そのまま)</option>
        <option value="date">日付</option>
        <option value="number">数値</option>
        <option value="boolean">Boolean(真偽値)</option>
      </select>
    </div>
    ${extraRowsHtml}`;
}

/// <summary>CSV列名(+extraTokensで指定した特殊トークン)を挿入ボタンとして並べたHTML。無ければ空文字を返す。</summary>
function buildVariableInsertRowHtml(extraTokens = []) {
  const tokens = [...extraTokens, ...csvHeaders];
  if (tokens.length === 0) return '';
  return `
  <div class="variable-insert-row">
    <span class="field-label">変数を挿入</span>
    <div class="variable-chip-list">
      ${tokens.map((t) => `<button type="button" class="variable-chip-btn" data-token="${escapeHtml(t)}">${escapeHtml(t)}</button>`).join('')}
    </div>
  </div>`;
}

/// <summary>変数挿入ボタンのクリックで、targetInputIdの要素のカーソル位置に"{トークン}"を挿入する。</summary>
function wireVariableInsertButtons(targetInputId, onInsert) {
  propsPanel.querySelectorAll('.variable-chip-btn').forEach((btn) => {
    btn.addEventListener('click', () => {
      const targetEl = document.getElementById(targetInputId);
      const insertText = `{${btn.dataset.token}}`;
      const start = targetEl.selectionStart ?? targetEl.value.length;
      const end = targetEl.selectionEnd ?? targetEl.value.length;
      targetEl.value = targetEl.value.slice(0, start) + insertText + targetEl.value.slice(end);
      onInsert(targetEl.value);
      targetEl.focus();
      targetEl.selectionStart = targetEl.selectionEnd = start + insertText.length;
    });
  });
}

/// <summary>数値の表示形式(小数桁数/桁区切り/接頭辞接尾辞)の入力欄にイベントを配線する。DataType=Number・計算(Calc)フィールドで共有。</summary>
function wireNumberFormatInputs(field) {
  document.getElementById('propNumberDecimalPlaces').addEventListener('input', (e) => {
    const v = e.target.value;
    field.numberDecimalPlaces = v === '' ? null : Math.max(0, parseInt(v, 10) || 0);
  });
  document.getElementById('propNumberDecimalPlaces').addEventListener('change', commitHistory);
  document.getElementById('propNumberThousands').addEventListener('change', (e) => {
    field.numberUseThousandsSeparator = e.target.checked;
    commitHistory();
  });
  document.getElementById('propNumberPrefix').addEventListener('input', (e) => { field.numberPrefix = e.target.value; });
  document.getElementById('propNumberPrefix').addEventListener('change', commitHistory);
  document.getElementById('propNumberSuffix').addEventListener('input', (e) => { field.numberSuffix = e.target.value; });
  document.getElementById('propNumberSuffix').addEventListener('change', commitHistory);
}

function renderPropsPanel() {
  const field = layout.fields.find((f) => f.id === selectedFieldId);
  if (!field) {
    propsPanel.innerHTML = '<div class="props-empty" id="propsEmptyHint">フィールドを選択すると設定が表示されます。</div>';
    return;
  }

  let sourceRowsHtml;
  if (field.kind === 'text') {
    sourceRowsHtml = `
    <div class="prop-row">
      <span class="field-label">テキスト内容(改行可)</span>
      <textarea class="text-input" id="propStaticText" rows="3">${escapeHtml(field.staticText ?? '')}</textarea>
      <div class="field-hint">CSVの列を <code>{列名}</code> の形式で埋め込むと、行ごとの値に置き換わります(例: こんにちは、{氏名}様)。</div>
      ${buildVariableInsertRowHtml()}
    </div>`;
  } else if (field.kind === 'calc') {
    sourceRowsHtml = `
    <div class="prop-row">
      <span class="field-label">計算式</span>
      <input type="text" class="text-input mono-input" id="propFormula" value="${escapeHtml(field.formula ?? '')}" placeholder="{単価}*{数量}" />
      <div class="field-hint">CSVの列を <code>{列名}</code>、行番号を <code>{行番号}</code> として使い、+ - * / ( ) の式が書けます。参照列が無い・0除算などの場合は #ERROR と表示されます。</div>
      ${buildVariableInsertRowHtml(['行番号'])}
    </div>
    ${buildNumberFormatRowsHtml(field)}`;
  } else {
    sourceRowsHtml = `
    <div class="prop-row">
      <span class="field-label">CSV列(データ取得元)</span>
      <select class="select-input" id="propColumn">${buildCsvColumnOptionsHtml(field.csvColumn)}</select>
    </div>
    <div class="prop-row">
      <span class="field-label">表示名(任意)</span>
      <input type="text" class="text-input" id="propLabel" value="${escapeHtml(field.label ?? '')}" placeholder="${escapeHtml(field.csvColumn)}" />
    </div>`;
  }

  const disabledIfLocked = field.locked ? 'disabled' : '';

  // データ型設定はCSV列由来のフィールドのみ意味を持つ(固定テキスト・計算フィールドは対象外)。
  const dataTypeSettingsHtml = field.kind === 'csv' ? buildDataTypeSettingsHtml(field) : '';

  propsPanel.innerHTML = `
    ${sourceRowsHtml}
    ${dataTypeSettingsHtml}
    <div class="prop-row">
      <label><input type="checkbox" id="propLocked" ${field.locked ? 'checked' : ''} /> 🔒 位置とサイズをロック(移動・リサイズを禁止)</label>
    </div>
    <div class="prop-row">
      <span class="field-label">X (pt)</span>
      <input type="number" step="0.5" class="text-input mono-input" id="propX" value="${field.x.toFixed(1)}" ${disabledIfLocked} />
    </div>
    <div class="prop-row">
      <span class="field-label">Y (pt)</span>
      <input type="number" step="0.5" class="text-input mono-input" id="propY" value="${field.y.toFixed(1)}" ${disabledIfLocked} />
    </div>
    <div class="prop-row">
      <span class="field-label">フォント</span>
      <select class="select-input" id="propFont">${buildFontOptionsHtml(field.fontFamily)}</select>
    </div>
    <div class="prop-row">
      <span class="field-label">サイズ (pt)</span>
      <input type="number" step="0.5" min="4" class="text-input mono-input" id="propSize" value="${field.fontSizePt}" />
    </div>
    <div class="prop-row">
      <span class="field-label">文字色</span>
      <input type="color" class="text-input" id="propColor" value="${field.color}" />
    </div>
    <div class="prop-row">
      <span class="field-label">背景色</span>
      <div class="prop-bg-row">
        <label><input type="checkbox" id="propBgEnabled" ${field.backgroundColor ? 'checked' : ''} /> 背景色を付ける</label>
        <input type="color" class="text-input" id="propBgColor" value="${field.backgroundColor ?? '#FFFFFF'}" ${field.backgroundColor ? '' : 'disabled'} />
      </div>
    </div>
    <div class="prop-row">
      <span class="field-label">横配置</span>
      <select class="select-input" id="propAlign">
        <option value="left">左揃え</option>
        <option value="center">中央揃え</option>
        <option value="right">右揃え</option>
      </select>
    </div>
    <div class="prop-row">
      <span class="field-label">縦配置</span>
      <select class="select-input" id="propVerticalAlign">
        <option value="top">上詰め</option>
        <option value="middle">中央揃え</option>
        <option value="bottom">下詰め</option>
      </select>
    </div>
    <div class="prop-row">
      <span class="field-label">はみ出し時</span>
      <select class="select-input" id="propOverflow">
        <option value="none">そのまま</option>
        <option value="shrink">自動縮小</option>
        <option value="wrap">折り返し</option>
        <option value="clip">切り詰め</option>
      </select>
    </div>
    <div class="prop-row" id="propMaxWidthRow">
      <span class="field-label">幅 (pt)</span>
      <input type="number" step="0.01" min="${MIN_FIELD_WIDTH_PT}" class="text-input mono-input" id="propMaxWidth" value="${field.maxWidthPt.toFixed(2)}" ${disabledIfLocked} />
    </div>
    <div class="prop-row" id="propHeightRow">
      <span class="field-label">高さ (pt)</span>
      <input type="number" step="0.01" min="${MIN_FIELD_HEIGHT_PT}" class="text-input mono-input" id="propHeight" value="${field.heightPt.toFixed(2)}" ${disabledIfLocked} />
    </div>
    <button class="btn btn-danger" id="propDeleteButton">このフィールドを削除</button>
  `;

  document.getElementById('propAlign').value = field.align;
  document.getElementById('propVerticalAlign').value = field.verticalAlign;
  document.getElementById('propOverflow').value = field.overflow;
  if (field.kind === 'csv') {
    document.getElementById('propDataType').value = field.dataType;
    if (field.dataType === 'date') {
      document.getElementById('propDateFormatKind').value = field.dateFormatKind;
    }
  }

  // 「input」はドラッグ中と同様にライブプレビューだけ行い、「change」(確定時)で履歴に積む。
  if (field.kind === 'text') {
    document.getElementById('propStaticText').addEventListener('input', (e) => {
      field.staticText = e.target.value;
      renderFieldChips();
    });
    document.getElementById('propStaticText').addEventListener('change', commitHistory);
    wireVariableInsertButtons('propStaticText', (value) => {
      field.staticText = value;
      renderFieldChips();
      commitHistory();
    });
  } else if (field.kind === 'calc') {
    document.getElementById('propFormula').addEventListener('input', (e) => {
      field.formula = e.target.value;
      renderFieldChips();
    });
    document.getElementById('propFormula').addEventListener('change', commitHistory);
    wireVariableInsertButtons('propFormula', (value) => {
      field.formula = value;
      renderFieldChips();
      commitHistory();
    });
    wireNumberFormatInputs(field);
  } else {
    document.getElementById('propColumn').addEventListener('change', (e) => {
      field.csvColumn = e.target.value;
      renderFieldChips();
      commitHistory();
    });
    document.getElementById('propLabel').addEventListener('input', (e) => {
      field.label = e.target.value || null;
      renderFieldChips();
    });
    document.getElementById('propLabel').addEventListener('change', commitHistory);

    // データ型設定(CSV列由来のフィールドのみ)。型を切り替えると設定行の構成が変わるためrenderPropsPanel()で再描画する。
    document.getElementById('propDataType').addEventListener('change', (e) => {
      field.dataType = e.target.value;
      renderPropsPanel();
      commitHistory();
    });
    if (field.dataType === 'date') {
      document.getElementById('propDateFormatKind').addEventListener('change', (e) => {
        field.dateFormatKind = e.target.value;
        renderPropsPanel();
        commitHistory();
      });
      const customFormatInput = document.getElementById('propDateCustomFormat');
      if (customFormatInput) {
        customFormatInput.addEventListener('input', (e) => { field.dateCustomFormat = e.target.value; });
        customFormatInput.addEventListener('change', commitHistory);
      }
    } else if (field.dataType === 'number') {
      wireNumberFormatInputs(field);
    } else if (field.dataType === 'boolean') {
      document.getElementById('propBooleanTrueValues').addEventListener('input', (e) => { field.booleanTrueValues = e.target.value; });
      document.getElementById('propBooleanTrueValues').addEventListener('change', commitHistory);
      document.getElementById('propBooleanTrueDisplay').addEventListener('input', (e) => { field.booleanTrueDisplay = e.target.value; });
      document.getElementById('propBooleanTrueDisplay').addEventListener('change', commitHistory);
      document.getElementById('propBooleanFalseDisplay').addEventListener('input', (e) => { field.booleanFalseDisplay = e.target.value; });
      document.getElementById('propBooleanFalseDisplay').addEventListener('change', commitHistory);
    }
  }
  document.getElementById('propLocked').addEventListener('change', (e) => {
    field.locked = e.target.checked;
    renderFieldChips();
    renderPropsPanel();
    commitHistory();
  });
  document.getElementById('propX').addEventListener('input', (e) => {
    field.x = parseFloat(e.target.value) || 0;
    renderFieldChips();
  });
  document.getElementById('propX').addEventListener('change', commitHistory);
  document.getElementById('propY').addEventListener('input', (e) => {
    field.y = parseFloat(e.target.value) || 0;
    renderFieldChips();
  });
  document.getElementById('propY').addEventListener('change', commitHistory);
  document.getElementById('propFont').addEventListener('change', (e) => {
    field.fontFamily = e.target.value;
    renderFieldChips();
    commitHistory();
  });
  document.getElementById('propSize').addEventListener('input', (e) => {
    field.fontSizePt = parseFloat(e.target.value) || 1;
    renderFieldChips();
  });
  document.getElementById('propSize').addEventListener('change', commitHistory);
  document.getElementById('propColor').addEventListener('input', (e) => {
    field.color = e.target.value;
    renderFieldChips();
  });
  document.getElementById('propColor').addEventListener('change', commitHistory);
  document.getElementById('propBgEnabled').addEventListener('change', (e) => {
    const bgColorInput = document.getElementById('propBgColor');
    field.backgroundColor = e.target.checked ? (bgColorInput.value || '#FFFFFF') : null;
    bgColorInput.disabled = !e.target.checked;
    renderFieldChips();
    commitHistory();
  });
  document.getElementById('propBgColor').addEventListener('input', (e) => {
    field.backgroundColor = e.target.value;
    renderFieldChips();
  });
  document.getElementById('propBgColor').addEventListener('change', commitHistory);
  document.getElementById('propAlign').addEventListener('change', (e) => {
    field.align = e.target.value;
    renderFieldChips();
    commitHistory();
  });
  document.getElementById('propVerticalAlign').addEventListener('change', (e) => {
    field.verticalAlign = e.target.value;
    renderFieldChips();
    commitHistory();
  });
  document.getElementById('propOverflow').addEventListener('change', (e) => {
    field.overflow = e.target.value;
    renderFieldChips();
    commitHistory();
  });
  document.getElementById('propMaxWidth').addEventListener('input', (e) => {
    field.maxWidthPt = roundTo2(Math.max(parseFloat(e.target.value) || DEFAULT_FIELD_WIDTH_PT, MIN_FIELD_WIDTH_PT));
    renderFieldChips();
  });
  document.getElementById('propMaxWidth').addEventListener('change', commitHistory);
  document.getElementById('propHeight').addEventListener('input', (e) => {
    field.heightPt = roundTo2(Math.max(parseFloat(e.target.value) || DEFAULT_FIELD_HEIGHT_PT, MIN_FIELD_HEIGHT_PT));
    renderFieldChips();
  });
  document.getElementById('propHeight').addEventListener('change', commitHistory);
  document.getElementById('propDeleteButton').addEventListener('click', () => {
    deleteField(field.id);
  });
}

// --- CSV読み込み ---
loadCsvButton.addEventListener('click', () => {
  csvModal.classList.remove('hidden');
});

csvCancelButton.addEventListener('click', () => csvModal.classList.add('hidden'));

csvBrowseButton.addEventListener('click', async () => {
  csvBrowseButton.disabled = true;
  try {
    const result = await browseCsvFile();
    if (result.path) {
      selectedCsvPath = result.path;
      csvPathDisplay.value = selectedCsvPath;
    }
  } catch (err) {
    showError(`ファイル選択に失敗しました: ${err.message}`);
  } finally {
    csvBrowseButton.disabled = false;
  }
});

csvConfirmButton.addEventListener('click', async () => {
  if (!selectedCsvPath) {
    showError('CSVファイルを選択してください。');
    return;
  }
  const encoding = csvEncodingSelect.value;
  const delimiter = csvDelimiterInput.value || ',';
  const hasHeader = csvHasHeaderCheckbox.checked;

  csvConfirmButton.disabled = true;
  try {
    const result = await loadCsvFromPath(selectedCsvPath, encoding, delimiter, hasHeader);
    csvSessionId = result.csvSessionId;
    csvHeaders = result.headers;
    csvRowCount = result.rowCount;
    currentRowIndex = 0;

    layout.csvSettings = { encoding, delimiter, hasHeader, lastFilePath: selectedCsvPath };
    // 次回プロジェクトを開いたときに自動再読込できるよう、選択したCSVパスをすぐに保存しておく。
    layout = await saveLayout(templateId, layout);

    renderPalette();
    renderFieldChips();
    renderPropsPanel();
    updateRowbar();
    togglePreviewButton.disabled = csvRowCount === 0;
    prevRowButton.disabled = csvRowCount === 0;
    nextRowButton.disabled = csvRowCount === 0;
    csvModal.classList.add('hidden');
  } catch (err) {
    showError(`CSVの読み込みに失敗しました: ${err.message}`);
  } finally {
    csvConfirmButton.disabled = false;
  }
});

// --- 行送りプレビュー ---
function updateRowbar() {
  rowbarText.textContent = csvRowCount === 0 ? 'CSV未読込' : `${currentRowIndex + 1} / ${csvRowCount} 行目`;
  prevRowButton.disabled = csvRowCount === 0 || currentRowIndex === 0;
  nextRowButton.disabled = csvRowCount === 0 || currentRowIndex >= csvRowCount - 1;
}

prevRowButton.addEventListener('click', async () => {
  if (currentRowIndex > 0) {
    currentRowIndex--;
    updateRowbar();
    if (isPreviewMode) await showPreview();
  }
});

nextRowButton.addEventListener('click', async () => {
  if (currentRowIndex < csvRowCount - 1) {
    currentRowIndex++;
    updateRowbar();
    if (isPreviewMode) await showPreview();
  }
});

togglePreviewButton.addEventListener('click', async () => {
  isPreviewMode = !isPreviewMode;
  togglePreviewButton.textContent = isPreviewMode ? '編集モードに戻す' : 'プレビュー切替';
  if (isPreviewMode) {
    await showPreview();
  } else {
    await renderTemplateBackground();
    renderFieldChips();
  }
});

async function showPreview() {
  try {
    const blob = await renderPreview(templateId, layout.fields, csvSessionId, currentRowIndex);
    const info = await renderPdfToCanvas(blob, pdfCanvas, displayScale);
    pdfStage.style.width = `${pdfCanvas.width}px`;
    pdfStage.style.height = `${pdfCanvas.height}px`;
    displayScale = info.displayScale;
    pdfStage.querySelectorAll('.field-chip').forEach((el) => el.remove());
  } catch (err) {
    showError(`プレビューの生成に失敗しました: ${err.message}`);
  }
}

// --- レイアウト保存 ---
saveLayoutButton.addEventListener('click', async () => {
  saveLayoutButton.disabled = true;
  try {
    layout = await saveLayout(templateId, layout);
    // layoutを保存結果(サーバーが返した新しいオブジェクト群)で丸ごと差し替えるため、
    // 表示中のチップ・プロパティパネルが古いフィールドオブジェクトへの参照を持ったままにならないよう再描画する。
    // (再描画しないと、保存直後に同じフィールドを選択し直さず編集した場合、その変更が次の保存で失われる)
    renderFieldChips();
    renderPropsPanel();
    const original = saveLayoutButton.textContent;
    saveLayoutButton.textContent = '保存しました';
    setTimeout(() => { saveLayoutButton.textContent = original; }, 1500);
  } catch (err) {
    showError(`レイアウトの保存に失敗しました: ${err.message}`);
  } finally {
    saveLayoutButton.disabled = false;
  }
});

// --- 出力設定画面へ ---
goToOutputButton.addEventListener('click', async () => {
  if (csvRowCount === 0 || !csvSessionId) {
    showError('出力するにはCSVを読み込んでください。');
    return;
  }

  goToOutputButton.disabled = true;
  try {
    layout = await saveLayout(templateId, layout); // 出力直前の状態を必ず保存してから進む
    sessionStorage.setItem('outputContext', JSON.stringify({
      templateId,
      csvSessionId,
      fields: layout.fields,
      rowCount: csvRowCount,
      outputSettings: layout.outputSettings
    }));
    window.location.href = 'output.html';
  } catch (err) {
    showError(`レイアウトの保存に失敗しました: ${err.message}`);
    goToOutputButton.disabled = false;
  }
});

init();
