namespace CsvPrintGokko.Core.Models;

/// <summary>CSVの文字エンコーディング。</summary>
public enum CsvEncoding
{
    ShiftJis,
    Utf8
}

/// <summary>フィールドの種類。CsvはCSVの列値、TextはCSV行に依存しない固定テキスト、Calcは計算式の評価結果を描画する。</summary>
public enum FieldKind
{
    Csv,
    Text,
    Calc
}

/// <summary>テンプレートの種類。SingleはCSV1行につき1ページの差込印刷、Listは全行を一覧表示する一覧表。</summary>
public enum TemplateKind
{
    Single,
    List
}

/// <summary>フィールドのテキスト配置(左揃え/中央揃え/右揃え)。</summary>
public enum TextAlign
{
    Left,
    Center,
    Right
}

/// <summary>フィールドのテキスト縦配置(上詰め/中央揃え/下詰め)。</summary>
public enum VerticalAlign
{
    Top,
    Middle,
    Bottom
}

/// <summary>テキストが指定幅を超えた場合の挙動。</summary>
public enum OverflowBehavior
{
    None,
    Shrink,
    Wrap,
    Clip
}

/// <summary>PDF出力モード(結合/個別)。TemplateKind.Singleのときのみ意味を持つ(Listは常に一覧表として1つのPDFに出力される)。</summary>
public enum OutputMode
{
    Combined,
    Individual
}

/// <summary>CSV値を描画前にどう解釈・整形するか。Textは無変換。</summary>
public enum DataType
{
    Text,
    Date,
    Number,
    Boolean
}

/// <summary>DataType.Dateのときの表示形式。</summary>
public enum DateFormatKind
{
    /// <summary>yyyy/MM/dd</summary>
    Slash,
    /// <summary>yyyy年MM月dd日</summary>
    Kanji,
    /// <summary>MM/dd</summary>
    MonthDay,
    /// <summary>和暦(例: 令和8年7月27日)</summary>
    Japanese,
    /// <summary>yyyy/MM/dd HH:mm</summary>
    SlashWithTime,
    /// <summary>yyyy年MM月dd日 HH時mm分</summary>
    KanjiWithTime,
    /// <summary>HH:mm(時刻のみ)</summary>
    TimeOnly,
    /// <summary>和暦+時刻(例: 令和8年7月27日 14時30分)</summary>
    JapaneseWithTime,
    /// <summary>DateCustomFormatに指定した.NET日付書式文字列をそのまま使う。</summary>
    Custom
}
