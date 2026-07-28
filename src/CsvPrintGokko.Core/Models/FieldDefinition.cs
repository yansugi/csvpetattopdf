namespace CsvPrintGokko.Core.Models;

/// <summary>PDFテンプレート上に配置する1つの描画設定(CSV列の値、または固定テキスト)。</summary>
public sealed record FieldDefinition
{
    public required Guid Id { get; init; }
    public FieldKind Kind { get; init; } = FieldKind.Csv;

    /// <summary>Kind=Csvのときに描画元となるCSV列名。Kind=Textのときは未使用。</summary>
    public string? CsvColumn { get; init; }

    /// <summary>Kind=Textのときに、CSVの行に関わらず常に描画される固定テキスト。</summary>
    public string? StaticText { get; init; }

    /// <summary>
    /// Kind=Calcのときに評価する計算式(例: "{単価}*{数量}")。
    /// "{列名}"でCSVの値、"{行番号}"で1始まりの行番号を参照でき、+ - * / ( ) の四則演算が使える。
    /// UseJavaScriptFormula=trueのときは使用しない。
    /// </summary>
    public string? Formula { get; init; }

    /// <summary>trueの場合、Kind=CalcのフィールドはFormulaの代わりにJavaScriptFormulaをJavaScript式として評価する。</summary>
    public bool UseJavaScriptFormula { get; init; }

    /// <summary>
    /// UseJavaScriptFormula=trueのときに評価するJavaScript式(例: "Number(row[\"単価\"]) * Number(row[\"数量\"])")。
    /// row[列名]でCSVの値(文字列)、rowNumberで1始まりの行番号を参照できる。
    /// </summary>
    public string? JavaScriptFormula { get; init; }

    /// <summary>配置エディタ上での表示名(任意、Kind=Csvのみ使用)。未設定ならCsvColumnをそのまま表示する。PDF出力には影響しない。</summary>
    public string? Label { get; init; }

    public required double X { get; init; }
    public required double Y { get; init; }
    public required string FontFamily { get; init; }
    public required double FontSizePt { get; init; }
    public required string Color { get; init; }

    /// <summary>背景色(#RRGGBB)。未設定(null)なら背景を描画しない(透明)。</summary>
    public string? BackgroundColor { get; init; }

    public required TextAlign Align { get; init; }
    public VerticalAlign VerticalAlign { get; init; } = VerticalAlign.Top;
    public OverflowBehavior Overflow { get; init; } = OverflowBehavior.None;

    /// <summary>Overflowが None 以外の場合に必須となる最大幅(pt)。</summary>
    public double? MaxWidthPt { get; init; }

    /// <summary>Overflowが None 以外の場合に意味を持つボックスの高さ(pt)。</summary>
    public double? HeightPt { get; init; }

    /// <summary>trueの場合、配置エディタ上での位置移動・サイズ変更を禁止する(PDF出力の見た目には影響しない)。</summary>
    public bool Locked { get; init; }

    /// <summary>CSV値の解釈・整形方法。Kind=Csvのみ意味を持つ(Kind=Textの固定テキストには適用しない)。</summary>
    public DataType DataType { get; init; } = DataType.Text;

    /// <summary>DataType=Dateのときの表示形式。</summary>
    public DateFormatKind DateFormatKind { get; init; } = DateFormatKind.Slash;

    /// <summary>DateFormatKind=Customのときに使う.NET日付書式文字列(例: "yyyy.MM.dd")。</summary>
    public string? DateCustomFormat { get; init; }

    /// <summary>DataType=Numberのときの小数点以下の桁数。未指定(null)なら丸めずそのままの桁数で表示する。</summary>
    public int? NumberDecimalPlaces { get; init; }

    /// <summary>DataType=Numberのときに3桁区切りのカンマを付けるかどうか。</summary>
    public bool NumberUseThousandsSeparator { get; init; }

    /// <summary>DataType=Numberのときに数値の前に付与する文字列(例: "¥")。</summary>
    public string? NumberPrefix { get; init; }

    /// <summary>DataType=Numberのときに数値の後に付与する文字列(例: "円")。</summary>
    public string? NumberSuffix { get; init; }

    /// <summary>DataType=Booleanのときに「真」とみなすCSV値のカンマ区切りリスト(大文字小文字・前後空白は無視)。</summary>
    public string BooleanTrueValues { get; init; } = "true,1,○,有,済";

    /// <summary>DataType=Booleanで真と判定されたときの表示文字列。</summary>
    public string BooleanTrueDisplay { get; init; } = "✓";

    /// <summary>DataType=Booleanで偽と判定されたときの表示文字列。</summary>
    public string BooleanFalseDisplay { get; init; } = "";
}
