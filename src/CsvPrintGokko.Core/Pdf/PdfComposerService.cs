using System.Globalization;
using System.Text;
using CsvPrintGokko.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// PDFテンプレートに配置フィールドの値を描画して1ページ分のPDFを合成するサービス。
/// プレビュー(Phase 3)と本番出力(Phase 5)の両方から共通で利用し、
/// 編集時の見た目と最終出力のズレが生まれないようにする。
/// </summary>
public sealed class PdfComposerService
{
    static PdfComposerService()
    {
        // GlobalFontSettings.FontResolverはプロセス内で一度しか設定できないため、
        // 未設定の場合のみ本実装のリゾルバを割り当てる。
        GlobalFontSettings.FontResolver ??= new WindowsFontResolver();
    }

    /// <summary>
    /// テンプレートPDF(1ページ目のみ使用)にfieldsの配置に従いrowDataの値を描画したPdfDocumentを返す。
    /// 呼び出し側でSaveやストリーム書き出しと破棄(Dispose)を行うこと。
    /// </summary>
    public PdfDocument ComposeSinglePage(
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyDictionary<string, string> rowData,
        int rowNumber = 1)
    {
        var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        if (document.PageCount == 0)
            throw new InvalidDataException("テンプレートPDFにページが含まれていません。");

        DrawFields(document.Pages[0], fields, rowData, rowNumber);
        return document;
    }

    /// <summary>
    /// 既存のPdfDocumentにテンプレートの1ページ目を複製したページを追加し、フィールドを描画する。
    /// 結合出力(全行を1つのPDFにまとめるモード)で使用する。
    /// </summary>
    public void AppendComposedPage(
        PdfDocument targetDocument,
        string templatePath,
        IReadOnlyList<FieldDefinition> fields,
        IReadOnlyDictionary<string, string> rowData,
        int rowNumber = 1)
    {
        using var templateDocument = PdfReader.Open(templatePath, PdfDocumentOpenMode.Import);
        if (templateDocument.PageCount == 0)
            throw new InvalidDataException("テンプレートPDFにページが含まれていません。");

        var importedPage = targetDocument.AddPage(templateDocument.Pages[0]);
        DrawFields(importedPage, fields, rowData, rowNumber);
    }

    private static void DrawFields(PdfPage page, IReadOnlyList<FieldDefinition> fields, IReadOnlyDictionary<string, string> rowData, int rowNumber)
    {
        using var gfx = XGraphics.FromPdfPage(page);
        foreach (var field in fields)
        {
            if (field.Kind == FieldKind.Text)
            {
                // 固定テキストでも"{列名}"はCSVの実データに置換してから描画する(それ以外の部分は行に依らず固定)。
                string resolvedText = TextVariableResolver.Resolve(field.StaticText ?? string.Empty, rowData);
                DrawField(gfx, page, field, resolvedText);
            }
            else if (field.Kind == FieldKind.Calc)
            {
                // 計算式の評価に失敗した場合(参照列が無い/0除算/構文エラー等)は#ERRORとして可視化する。
                string calcText = FormulaEvaluator.TryEvaluate(field.Formula ?? string.Empty, rowData, rowNumber, out double calcResult)
                    ? CsvValueFormatter.FormatNumberValue(field, calcResult)
                    : "#ERROR";
                DrawField(gfx, page, field, calcText);
            }
            // CSVに対応する列が無い場合は描画をスキップする(列マッピングの不整合はPhase 4のUIで警告する想定)。
            else if (field.CsvColumn is not null && rowData.TryGetValue(field.CsvColumn, out var text) && text is not null)
            {
                DrawField(gfx, page, field, CsvValueFormatter.Format(field, text));
            }
        }
    }

    private static void DrawField(XGraphics gfx, PdfPage page, FieldDefinition field, string text)
    {
        var brush = new XSolidBrush(ParseHexColor(field.Color));
        double effectiveWidth = field.MaxWidthPt ?? Math.Max(page.Width.Point - field.X, 10.0);
        var format = BuildStringFormat(field.Align, field.VerticalAlign);
        // 明示的な改行(\n)を段落として扱う。自由テキストの複数行入力や、CSV値に改行が含まれる場合に対応する。
        var explicitLines = SplitExplicitLines(text);

        switch (field.Overflow)
        {
            case OverflowBehavior.Shrink:
            {
                var font = ShrinkToFit(gfx, explicitLines, field.FontFamily, field.FontSizePt, effectiveWidth, field.HeightPt);
                DrawBackground(gfx, field, effectiveWidth, field.HeightPt ?? font.Height * explicitLines.Count);
                DrawLineBlock(gfx, explicitLines, font, brush, field.X, field.Y, effectiveWidth, field.HeightPt, truncateToHeight: false, field.VerticalAlign, format);
                break;
            }
            case OverflowBehavior.Wrap:
            {
                var font = new XFont(field.FontFamily, field.FontSizePt);
                var wrappedLines = explicitLines
                    .SelectMany(paragraph => paragraph.Length == 0
                        ? new[] { string.Empty }
                        : WrapLines(gfx, paragraph, font, effectiveWidth).ToArray())
                    .ToList();
                DrawBackground(gfx, field, effectiveWidth, field.HeightPt ?? font.Height * wrappedLines.Count);
                DrawLineBlock(gfx, wrappedLines, font, brush, field.X, field.Y, effectiveWidth, field.HeightPt, truncateToHeight: true, field.VerticalAlign, format);
                break;
            }
            case OverflowBehavior.Clip:
            {
                var font = new XFont(field.FontFamily, field.FontSizePt);
                double height = field.HeightPt ?? font.Height * explicitLines.Count;
                DrawBackground(gfx, field, effectiveWidth, height);
                var state = gfx.Save();
                gfx.IntersectClip(new XRect(field.X, field.Y, effectiveWidth, height));
                DrawLineBlock(gfx, explicitLines, font, brush, field.X, field.Y, effectiveWidth, field.HeightPt, truncateToHeight: false, field.VerticalAlign, format);
                gfx.Restore(state);
                break;
            }
            default: // OverflowBehavior.None
            {
                var font = new XFont(field.FontFamily, field.FontSizePt);
                double height = field.HeightPt ?? font.Height * explicitLines.Count;
                DrawBackground(gfx, field, effectiveWidth, height);
                DrawLineBlock(gfx, explicitLines, font, brush, field.X, field.Y, effectiveWidth, field.HeightPt, truncateToHeight: false, field.VerticalAlign, format);
                break;
            }
        }
    }

    private static IReadOnlyList<string> SplitExplicitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

    /// <summary>背景色(BackgroundColor)が指定されている場合のみ、テキスト描画前にボックス全体を塗りつぶす。</summary>
    private static void DrawBackground(XGraphics gfx, FieldDefinition field, double width, double height)
    {
        if (field.BackgroundColor is null) return;
        gfx.DrawRectangle(new XSolidBrush(ParseHexColor(field.BackgroundColor)), field.X, field.Y, width, height);
    }

    /// <summary>横配置・縦配置の組み合わせをPDFsharpのXStringFormatに変換する。</summary>
    private static XStringFormat BuildStringFormat(TextAlign align, VerticalAlign verticalAlign) => (align, verticalAlign) switch
    {
        (TextAlign.Left, VerticalAlign.Top) => XStringFormats.TopLeft,
        (TextAlign.Center, VerticalAlign.Top) => XStringFormats.TopCenter,
        (TextAlign.Right, VerticalAlign.Top) => XStringFormats.TopRight,
        (TextAlign.Left, VerticalAlign.Middle) => XStringFormats.CenterLeft,
        (TextAlign.Center, VerticalAlign.Middle) => XStringFormats.Center,
        (TextAlign.Right, VerticalAlign.Middle) => XStringFormats.CenterRight,
        (TextAlign.Left, VerticalAlign.Bottom) => XStringFormats.BottomLeft,
        (TextAlign.Center, VerticalAlign.Bottom) => XStringFormats.BottomCenter,
        (TextAlign.Right, VerticalAlign.Bottom) => XStringFormats.BottomRight,
        _ => XStringFormats.TopLeft
    };

    /// <summary>全行が幅・高さ(高さ未指定なら行数分の高さ)に収まるまでフォントサイズを段階的に縮小する(下限4pt)。</summary>
    private static XFont ShrinkToFit(XGraphics gfx, IReadOnlyList<string> lines, string fontFamily, double startSizePt, double maxWidthPt, double? maxHeightPt)
    {
        const double minSizePt = 4.0;
        double size = startSizePt;
        while (size > minSizePt)
        {
            var font = new XFont(fontFamily, size);
            bool fitsWidth = lines.All(line => gfx.MeasureString(line, font).Width <= maxWidthPt);
            bool fitsHeight = maxHeightPt is null || font.Height * lines.Count <= maxHeightPt.Value;
            if (fitsWidth && fitsHeight)
                return font;
            size -= 0.5;
        }
        return new XFont(fontFamily, minSizePt);
    }

    /// <summary>
    /// 行の配列をVerticalAlignに従って上詰め/中央揃え/下詰めで配置して描画する。
    /// truncateToHeightがtrueの場合、高さ指定があれば収まる行数まで切り詰める(折り返しモード用)。
    /// </summary>
    private static void DrawLineBlock(
        XGraphics gfx, IReadOnlyList<string> lines, XFont font, XBrush brush,
        double x, double y, double maxWidthPt, double? maxHeightPt, bool truncateToHeight,
        VerticalAlign verticalAlign, XStringFormat format)
    {
        var visibleLines = lines;
        if (truncateToHeight && maxHeightPt is not null)
        {
            int maxLineCount = Math.Max(1, (int)(maxHeightPt.Value / font.Height));
            if (visibleLines.Count > maxLineCount)
                visibleLines = visibleLines.Take(maxLineCount).ToList();
        }

        double blockHeight = visibleLines.Count * font.Height;
        double startY = y;
        if (maxHeightPt is not null)
        {
            double slack = Math.Max(maxHeightPt.Value - blockHeight, 0);
            startY = verticalAlign switch
            {
                VerticalAlign.Middle => y + slack / 2,
                VerticalAlign.Bottom => y + slack,
                _ => y
            };
        }

        double cursorY = startY;
        foreach (string line in visibleLines)
        {
            gfx.DrawString(line, font, brush, new XRect(x, cursorY, maxWidthPt, font.Height), format);
            cursorY += font.Height;
        }
    }

    /// <summary>日本語は単語区切りが無いため、1文字ずつ幅を測って折り返す簡易実装。</summary>
    private static IEnumerable<string> WrapLines(XGraphics gfx, string text, XFont font, double maxWidthPt)
    {
        var current = new StringBuilder();
        foreach (char c in text)
        {
            string candidate = current.ToString() + c;
            if (current.Length > 0 && gfx.MeasureString(candidate, font).Width > maxWidthPt)
            {
                yield return current.ToString();
                current.Clear();
            }
            current.Append(c);
        }
        if (current.Length > 0)
            yield return current.ToString();
    }

    private static XColor ParseHexColor(string hex)
    {
        string h = hex.TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            throw new FormatException($"不正な色コードです(#RRGGBB形式で指定してください): {hex}");

        byte r = (byte)((rgb >> 16) & 0xFF);
        byte g = (byte)((rgb >> 8) & 0xFF);
        byte b = (byte)(rgb & 0xFF);
        return XColor.FromArgb(r, g, b);
    }
}
