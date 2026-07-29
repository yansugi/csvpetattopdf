using CsvPrintGokko.Core.Pdf;

namespace CsvPrintGokko.Core.Tests.Pdf;

public sealed class TablePaginationTests
{
    [Theory]
    [InlineData(0, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(100, 20, 5)]
    [InlineData(101, 20, 6)]
    public void CalculateTotalPages_総行数と1ページあたりの行数からページ数を求める(int totalRowCount, int rowsPerPage, int expected)
    {
        Assert.Equal(expected, TablePagination.CalculateTotalPages(totalRowCount, rowsPerPage));
    }

    [Fact]
    public void CalculateTotalPages_1ページあたりの行数が0以下なら例外()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TablePagination.CalculateTotalPages(10, 0));
    }
}
