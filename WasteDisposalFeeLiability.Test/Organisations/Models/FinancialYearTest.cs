using WasteDisposalFeeLiability.Organisations.Models;

namespace WasteDisposalFeeLiability.Test.Organisations.Models;

public class FinancialYearTest
{
    [Theory]
    [InlineData("2024-25")]
    [InlineData("2099-00")]
    public void TryParse_accepts_valid_format(string value)
    {
        var result = FinancialYear.TryParse(value, out var financialYear);

        Assert.True(result);
        Assert.Equal(value, financialYear.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2024")]
    [InlineData("2024/25")]
    [InlineData("2024-26")]
    [InlineData("1999-00")]
    [InlineData(null)]
    public void TryParse_rejects_invalid_format(string? value)
    {
        var result = FinancialYear.TryParse(value, out _);

        Assert.False(result);
    }

    [Fact]
    public void ToString_returns_underlying_value()
    {
        var financialYear = new FinancialYear("2024-25");

        Assert.Equal("2024-25", financialYear.ToString());
    }
}
