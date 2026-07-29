# CSVペタっとPDF

CSVの各行データをPDFテンプレート上の指定位置に差し込み、PDFを一括生成するWindows向けの完全オフライン・デスクトップツールです。

CSVをテンプレートPDFに「ペタっと」貼り付けて出力する、という発想からこの名前になりました。

## 特徴

- **完全オフライン動作** — インターネット接続不要。個人情報を含むCSVを外部に送信しません。
- **ローカル完結型Webアプリ** — 実行するとASP.NET Core(Kestrel)がループバックアドレス(`127.0.0.1`)のみで起動し、既定のブラウザでUIが開きます。外部からはアクセスできません。
- **単票・一覧表の両対応** — CSV1行につき1ページを生成する「単票」モードと、CSV全行を1つの表にまとめる「一覧表」モードを切り替えられます。
- **柔軟な文字・数式** — 自由テキスト、CSV列の差し込みに加え、四則演算の計算式、より高度なJavaScript式(Jint)による計算フィールドにも対応。
- **配布はexe1つ** — `dotnet publish`でWindows向け自己完結型の単一exeとして書き出せます。

## 主な機能

- PDFテンプレートへのドラッグ&ドロップでのフィールド配置(自由テキスト・CSV列・計算式)
- CSV読み込み(UTF-8 / Shift-JIS、区切り文字・ヘッダー有無を指定可能)、行送りプレビュー
- 出力時に使える変数: `{行番号}` `{ページ番号}` `{総ページ数}` `{出力時間}`
- 出力ファイル名パターンへの変数挿入(CSV列名・行番号・出力時間をボタンで挿入)
- 出力モード: 1つのPDFに結合 / 行ごとに個別ファイル
- プロジェクトのエクスポート・インポート(`.cpgproj`) — レイアウト・テンプレートPDF・CSVを1ファイルにまとめて別環境へ持ち出せる
- 名前を付けて保存(現在編集中の内容をPDFごと新しいプロジェクトとして複製)
- 背景PDFの差し替え(ページサイズが変わった場合は警告)
- プロジェクト名のいつでも変更可能な編集(リネーム)
- ブラウザを閉じるとバックグラウンドプロセスも自動的に終了(ハートビート監視)

## 動作環境

- Windows 10 / 11
- 配布用exeは自己完結型のため、利用者側で.NETランタイムのインストールは不要
- 開発には .NET 8 SDK(`net8.0-windows`)が必要

## プロジェクト構成

```
csvprintgokko/
├── src/
│   ├── CsvPrintGokko.App/       ASP.NET Core本体(Kestrel起動・APIエンドポイント・フロントエンド)
│   │   ├── Endpoints/            テンプレート・CSV・プレビュー・出力・ダイアログ等のAPI
│   │   ├── Services/             STAダイアログ・ハートビート監視などのサービス
│   │   └── wwwroot/              ビルドレスの素のHTML/CSS/JSフロントエンド
│   └── CsvPrintGokko.Core/      UI/HTTPに依存しない業務ロジック(class library)
│       ├── Csv/                  CSV読み込み
│       ├── Pdf/                  PDF合成・フォント解決・計算式評価
│       ├── Templates/            テンプレートの永続化(エクスポート/インポート含む)
│       ├── Output/               出力ファイル名パターン
│       └── Jobs/                 出力ジョブの実行
├── tests/
│   └── CsvPrintGokko.Core.Tests/ xUnitテスト(Coreのみが対象)
└── icon/                         アプリアイコン・favicon用の元画像
```

## 使い方(開発)

```powershell
# ビルド
dotnet build

# テスト実行
dotnet test

# 起動(既定のブラウザが自動的に開きます)
dotnet run --project src/CsvPrintGokko.App/CsvPrintGokko.App.csproj
```

起動すると `http://127.0.0.1:48923` でKestrelがリッスンし、ブラウザが自動的に開きます。ブラウザを全て閉じると、バックグラウンドプロセスも数秒〜90秒程度で自動的に終了します。

## 配布用パッケージング

```powershell
dotnet publish src/CsvPrintGokko.App/CsvPrintGokko.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o dist
```

`dist` フォルダ一式(`CsvPrintGokko.App.exe` + `wwwroot` 等)をそのままzip化して配布します。`CsvPrintGokko.App.exe` をダブルクリックするだけで起動します。

## 技術スタック

- ASP.NET Core Minimal API + Kestrel(ループバック固定)
- [PDFsharp](https://www.pdfsharp.net/) — PDF生成・合成
- [Jint](https://github.com/sebastienros/jint) — JavaScript式による計算フィールドの評価
- [CsvHelper](https://joshclose.github.io/CsvHelper/) — CSV読み込み
- pdf.js — ブラウザ側でのPDFプレビュー描画
- Monaco Editor — JavaScript計算式エディタ
- フロントエンドはビルドステップ無しの素のHTML/CSS/JS

## テストの範囲

`CsvPrintGokko.Core.Tests`(xUnit)のみで、UIに依存しない業務ロジック(CSVパース・PDF合成・ファイル名パターン・テンプレート永続化など)を対象としています。UI自動テストは導入しておらず、画面の動作確認は手動またはPlaywrightでの都度確認で行っています。
