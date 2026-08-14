using System.Globalization;
using PdfSharp.Drawing;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// PDF描画で使う#RRGGBB形式の色コードをXColorへ変換する共通ヘルパー。
/// PdfComposerService(通常のフィールド)とQrCodeRenderer(QRコードの前景色・背景色)の両方から利用する。
/// </summary>
internal static class HexColorParser
{
    public static XColor Parse(string hex)
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
