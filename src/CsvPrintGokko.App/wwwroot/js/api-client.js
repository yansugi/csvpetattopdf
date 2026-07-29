// バックエンドAPIの薄いラッパー。エラー時はレスポンス本文をメッセージにしたErrorを投げる。

async function assertOk(response) {
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `通信エラーが発生しました(HTTP ${response.status})`);
  }
  return response;
}

export async function listTemplates() {
  const res = await assertOk(await fetch('/api/templates'));
  return res.json();
}

export async function getTemplate(templateId) {
  const res = await assertOk(await fetch(`/api/templates/${templateId}`));
  return res.json();
}

export async function createTemplate(name, pdfFile, kind = 'single') {
  const form = new FormData();
  form.append('name', name);
  form.append('file', pdfFile);
  form.append('kind', kind);
  const res = await assertOk(await fetch('/api/templates', { method: 'POST', body: form }));
  return res.json();
}

export async function deleteTemplate(templateId) {
  await assertOk(await fetch(`/api/templates/${templateId}`, { method: 'DELETE' }));
}

/// <summary>プロジェクト一式(レイアウト+PDF+CSV)のエクスポートファイルをダウンロードするURL。</summary>
export function exportProjectUrl(templateId) {
  return `/api/templates/${templateId}/export`;
}

/// <summary>エクスポートしたプロジェクトファイルをアップロードし、新規テンプレートとして登録する。</summary>
export async function importProject(projectFile) {
  const form = new FormData();
  form.append('file', projectFile);
  const res = await assertOk(await fetch('/api/templates/import', { method: 'POST', body: form }));
  return res.json();
}

export async function saveLayout(templateId, layout) {
  const res = await assertOk(await fetch(`/api/templates/${templateId}/layout`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(layout)
  }));
  return res.json();
}

export function templatePdfUrl(templateId) {
  return `/api/templates/${templateId}/pdf`;
}

/// <summary>既存テンプレートのPDF実体を新しいPDFに差し替える。</summary>
export async function replaceTemplatePdf(templateId, pdfFile) {
  const form = new FormData();
  form.append('file', pdfFile);
  const res = await assertOk(await fetch(templatePdfUrl(templateId), { method: 'POST', body: form }));
  return res.json();
}

/// <summary>編集中のレイアウトをPDFごと新しいテンプレートとして複製保存する(名前を付けて保存)。</summary>
export async function saveAsTemplate(templateId, layout) {
  const res = await assertOk(await fetch(`/api/templates/${templateId}/save-as`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(layout)
  }));
  return res.json();
}

export async function browseCsvFile() {
  const res = await assertOk(await fetch('/api/dialogs/browse-csv-file', { method: 'POST' }));
  return res.json();
}

export async function loadCsvFromPath(path, encoding, delimiter, hasHeader) {
  const res = await assertOk(await fetch('/api/csv/load-from-path', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ path, encoding, delimiter, hasHeader })
  }));
  return res.json();
}

export async function getCsvRow(csvSessionId, index) {
  const res = await assertOk(await fetch(`/api/csv/${csvSessionId}/row/${index}`));
  return res.json();
}

/// <summary>CSVセッションの列名一覧だけを取得する(出力設定画面のファイル名変数ボタン用)。</summary>
export async function getCsvHeaders(csvSessionId) {
  const res = await assertOk(await fetch(`/api/csv/${csvSessionId}/headers`));
  return res.json();
}

export async function renderPreview(templateId, fields, csvSessionId, rowIndex) {
  const res = await assertOk(await fetch('/api/preview/render', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ templateId, fields, csvSessionId, rowIndex })
  }));
  return res.blob();
}

/// <summary>一覧表テンプレート(TemplateKind.List)専用のプレビュー。行送りではなくCSVの先頭から一部の行を使う。</summary>
export async function renderListPreview(templateId, fields, csvSessionId, listSettings) {
  const res = await assertOk(await fetch('/api/preview/render-list', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ templateId, fields, csvSessionId, listSettings })
  }));
  return res.blob();
}

/// <summary>JavaScript式エディタの「実行してテスト」用。csvSessionIdがnullなら空データで評価する。</summary>
export async function testJsFormula(script, csvSessionId, rowIndex) {
  const res = await assertOk(await fetch('/api/formula/test-js', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ script, csvSessionId: csvSessionId ?? null, rowIndex: rowIndex ?? null })
  }));
  return res.json();
}
