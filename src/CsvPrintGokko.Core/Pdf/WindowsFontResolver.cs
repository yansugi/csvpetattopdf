using System.Buffers.Binary;
using Microsoft.Win32;
using PdfSharp.Fonts;

namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// Windowsにインストール済みのフォントをレジストリから解決するIFontResolver実装。
/// Windowsの日本語フォント(游ゴシック等)の多くは.ttc(TrueType Collection)で配布されており、
/// PDFsharpのOpenTypeパーサはttcヘッダを直接解釈できずNullReferenceExceptionになることが
/// Phase 0のスパイク検証で判明したため、該当する場合は1フェイス分を単独のsfntバイナリとして
/// 抽出してから返す。
/// </summary>
public sealed class WindowsFontResolver : IFontResolver
{
    public byte[] GetFont(string faceName)
    {
        string? path = ResolveFontFilePath(faceName);
        if (path is null)
            throw new FileNotFoundException($"フォントファイルを解決できませんでした: {faceName}");

        byte[] bytes = File.ReadAllBytes(path);
        return IsTrueTypeCollection(bytes) ? ExtractSingleFaceFromTtc(bytes, faceIndex: 0) : bytes;
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // 太字/斜体のフェイス切り替えは行わず、常に指定ファミリー名をそのまま解決する。
        return new FontResolverInfo(familyName);
    }

    /// <summary>
    /// レジストリ(HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts)から
    /// "familyName (TrueType)" 形式のキーを探し、Fontsフォルダ内の実ファイルパスを返す。
    /// </summary>
    public static string? ResolveFontFilePath(string familyName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        if (key is null) return null;

        string fontsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        foreach (string valueName in key.GetValueNames())
        {
            if (valueName.StartsWith(familyName, StringComparison.OrdinalIgnoreCase)
                && key.GetValue(valueName) is string fileName)
            {
                return Path.Combine(fontsDir, fileName);
            }
        }
        return null;
    }

    private static bool IsTrueTypeCollection(byte[] bytes) =>
        bytes.Length >= 4 && bytes[0] == 't' && bytes[1] == 't' && bytes[2] == 'c' && bytes[3] == 'f';

    /// <summary>
    /// TrueType Collection(.ttc)から指定フェイスのテーブル群を集め、
    /// 単独のsfnt(TrueType/OpenType)バイナリとして再構築する。
    /// 仕様: https://learn.microsoft.com/en-us/typography/opentype/spec/otff#ttc-header
    /// </summary>
    private static byte[] ExtractSingleFaceFromTtc(byte[] ttc, int faceIndex)
    {
        uint numFonts = BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(8, 4));
        if (faceIndex >= numFonts)
            throw new ArgumentOutOfRangeException(nameof(faceIndex), $"ttc内のフェイス数({numFonts})を超えるインデックスが指定されました。");

        uint faceDirectoryOffset = BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(12 + faceIndex * 4, 4));

        uint sfntVersion = BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan((int)faceDirectoryOffset, 4));
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(ttc.AsSpan((int)faceDirectoryOffset + 4, 2));

        // 元のテーブルレコード(tag, checksum, offset, length)を読み出す。
        var records = new (uint Tag, uint CheckSum, uint Offset, uint Length)[numTables];
        for (int i = 0; i < numTables; i++)
        {
            int recOffset = (int)faceDirectoryOffset + 12 + i * 16;
            records[i] = (
                Tag: BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(recOffset, 4)),
                CheckSum: BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(recOffset + 4, 4)),
                Offset: BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(recOffset + 8, 4)),
                Length: BinaryPrimitives.ReadUInt32BigEndian(ttc.AsSpan(recOffset + 12, 4))
            );
        }

        // sfntディレクトリのsearchRange/entrySelector/rangeShiftを仕様通り再計算する。
        int entrySelector = (int)Math.Log2(numTables);
        int searchRange = (1 << entrySelector) * 16;
        int rangeShift = numTables * 16 - searchRange;

        using var output = new MemoryStream();
        using (var writer = new BigEndianWriter(output))
        {
            writer.WriteUInt32(sfntVersion);
            writer.WriteUInt16(numTables);
            writer.WriteUInt16((ushort)searchRange);
            writer.WriteUInt16((ushort)entrySelector);
            writer.WriteUInt16((ushort)rangeShift);

            int directoryEnd = 12 + numTables * 16;
            var newOffsets = new uint[numTables];
            uint cursor = (uint)directoryEnd;
            for (int i = 0; i < numTables; i++)
            {
                newOffsets[i] = cursor;
                uint paddedLength = (records[i].Length + 3) & ~3u; // 4バイト境界に切り上げ
                cursor += paddedLength;
            }

            // テーブルディレクトリ本体(新オフセットを反映)
            for (int i = 0; i < numTables; i++)
            {
                writer.WriteUInt32(records[i].Tag);
                writer.WriteUInt32(records[i].CheckSum);
                writer.WriteUInt32(newOffsets[i]);
                writer.WriteUInt32(records[i].Length);
            }

            // 各テーブルの実データをttc本体からコピーし、4バイト境界までゼロ埋めする。
            for (int i = 0; i < numTables; i++)
            {
                output.Write(ttc, (int)records[i].Offset, (int)records[i].Length);
                int pad = (int)((4 - records[i].Length % 4) % 4);
                for (int p = 0; p < pad; p++) output.WriteByte(0);
            }
        }

        return output.ToArray();
    }

    /// <summary>TTF/OTFのビッグエンディアン数値をMemoryStreamへ書き込む小さなヘルパー。</summary>
    private sealed class BigEndianWriter(Stream stream) : IDisposable
    {
        public void WriteUInt16(ushort value)
        {
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buf, value);
            stream.Write(buf);
        }

        public void WriteUInt32(uint value)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, value);
            stream.Write(buf);
        }

        public void Dispose() { }
    }
}
