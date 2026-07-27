// pdf.js統合と、画面px座標⇔PDF pt座標の変換をまとめたモジュール。
// pdf.jsのpage.getViewport({scale:1})はPDFのpt単位・top-left/y-down原点に一致するため
// (Phase 0で実測確認済み)、変換は displayScale による単純な乗除算のみで済む。

import * as pdfjsLib from '../lib/pdfjs/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc = new URL('../lib/pdfjs/pdf.worker.min.mjs', import.meta.url).href;

/**
 * PDFデータ(ArrayBufferまたはBlob)を指定canvasに描画する。
 * @returns {Promise<{pageWidthPt:number, pageHeightPt:number, displayScale:number}>}
 */
export async function renderPdfToCanvas(source, canvas, displayScale = 1.3) {
  const data = source instanceof Blob ? await source.arrayBuffer() : source;
  const pdf = await pdfjsLib.getDocument({ data }).promise;
  const page = await pdf.getPage(1);

  const basePt = page.getViewport({ scale: 1 });
  const viewport = page.getViewport({ scale: displayScale });

  canvas.width = viewport.width;
  canvas.height = viewport.height;
  const context = canvas.getContext('2d');
  await page.render({ canvasContext: context, viewport }).promise;

  return { pageWidthPt: basePt.width, pageHeightPt: basePt.height, displayScale };
}

export function pxToPt(px, displayScale) {
  return px / displayScale;
}

export function ptToPx(pt, displayScale) {
  return pt * displayScale;
}
