namespace CsvPrintGokko.Core.Models;

/// <summary>
/// TemplateKind.Listの一覧表示に関する設定。「繰り返し行の枠」(RowOriginY〜RowOriginY+RowHeightPtの
/// Y座標帯)を1行分の領域として定義し、この枠内にY座標を持つフィールドは自動的に繰り返し対象になる
/// (フィールド単位でのON/OFF設定は持たない)。
/// </summary>
public sealed record ListRenderSettings
{
    /// <summary>繰り返し行の枠の上端Y座標(pt)。この位置からRowHeightPt分の帯が「1行分の枠」になる。</summary>
    public double RowOriginY { get; init; } = 100;

    /// <summary>繰り返し行の枠の高さ(pt)。CSV1行ごとにこの高さ分だけYをずらしながら描画する。</summary>
    public double RowHeightPt { get; init; } = 20;

    /// <summary>
    /// 1ページに描画する繰り返し行数。CSVの行数がこれを超える場合は2ページ目以降へ自動的に続きを出力する。
    /// 配置エディタのキャンバス上のプレビュー行数にも、この値がそのまま使われる。
    /// </summary>
    public int RepeatCount { get; init; } = 8;

    /// <summary>trueの場合、配置エディタのキャンバス上で繰り返し行の枠のドラッグ移動・リサイズを禁止する(PDF出力の見た目には影響しない)。</summary>
    public bool Locked { get; init; }
}
