namespace CsvPrintGokko.Core.Pdf;

/// <summary>
/// 一覧表出力(TemplateKind.List)の改ページ計算。PDF描画から切り離した純粋な計算ロジックとしてテストしやすくする。
/// </summary>
public static class TablePagination
{
    /// <summary>総行数と1ページあたりの行数から、必要なページ数を求める(0行でも最低1ページ)。</summary>
    public static int CalculateTotalPages(int totalRowCount, int rowsPerPage)
    {
        if (rowsPerPage <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowsPerPage), "1ページあたりの行数は正の値である必要があります。");
        if (totalRowCount <= 0)
            return 1;

        return (int)Math.Ceiling(totalRowCount / (double)rowsPerPage);
    }
}
