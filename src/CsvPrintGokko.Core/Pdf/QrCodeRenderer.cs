using CsvPrintGokko.Core.Models;
using PdfSharp.Drawing;
using QRCoder;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// QRコードフィールド(FieldKind.Qr)を描画するサービス。QRCoderで生成したモジュール行列(白黒のマス目)を
/// そのままXGraphicsの矩形描画で再現する(Bitmap/PNG等の画像化は経由しない)。ベクター描画のため
/// 拡大縮小してもにじまず、System.Drawing.Common等の画像処理APIにも依存しない。
/// </summary>
public static class QrCodeRenderer
{
    /// <summary>MaxWidthPt未指定時に使う既定の一辺のサイズ(pt)。</summary>
    private const double DefaultSizePt = 80.0;

    /// <summary>
    /// field.X, field.Yを左上とする一辺field.MaxWidthPt(未指定ならDefaultSizePt)の正方形に、
    /// contentをエンコードしたQRコードを描画する。文字数超過等でQRコード化できない場合はfalseを返す
    /// (呼び出し側で計算式フィールドと同様の"#ERROR"表示等にフォールバックすることを想定)。
    /// </summary>
    public static bool TryDraw(XGraphics gfx, FieldDefinition field, string content, out string? errorMessage)
    {
        double sizePt = field.MaxWidthPt ?? DefaultSizePt;
        if (sizePt <= 0)
        {
            errorMessage = "#ERROR";
            return false;
        }

        QRCodeData qrData;
        try
        {
            using var generator = new QRCodeGenerator();
            qrData = generator.CreateQrCode(content, ToEccLevel(field.QrErrorCorrectionLevel));
        }
        catch (Exception)
        {
            // 文字数が上限(QRコード仕様上の最大バージョン)を超える等でエンコードできない場合、
            // PDF全体の生成は継続し、このフィールドだけエラー表示にフォールバックする。
            errorMessage = "#ERROR";
            return false;
        }

        var foregroundBrush = new XSolidBrush(HexColorParser.Parse(field.Color));
        if (field.BackgroundColor is not null)
            gfx.DrawRectangle(new XSolidBrush(HexColorParser.Parse(field.BackgroundColor)), field.X, field.Y, sizePt, sizePt);

        int moduleCount = qrData.ModuleMatrix.Count;
        double moduleSizePt = sizePt / moduleCount;
        for (int row = 0; row < moduleCount; row++)
        {
            var moduleRow = qrData.ModuleMatrix[row];
            for (int col = 0; col < moduleCount; col++)
            {
                if (!moduleRow[col]) continue;
                gfx.DrawRectangle(foregroundBrush, field.X + col * moduleSizePt, field.Y + row * moduleSizePt, moduleSizePt, moduleSizePt);
            }
        }

        errorMessage = null;
        return true;
    }

    private static QRCodeGenerator.ECCLevel ToEccLevel(QrErrorCorrectionLevel level) => level switch
    {
        QrErrorCorrectionLevel.Low => QRCodeGenerator.ECCLevel.L,
        QrErrorCorrectionLevel.Quartile => QRCodeGenerator.ECCLevel.Q,
        QrErrorCorrectionLevel.High => QRCodeGenerator.ECCLevel.H,
        _ => QRCodeGenerator.ECCLevel.M
    };
}
