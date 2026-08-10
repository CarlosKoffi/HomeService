using HomeService.Application.CompanyPortal;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPayoutFeeCalculatorTests
{
    [Theory]
    [InlineData(10_000, 150)]
    [InlineData(33_333, 500)]
    public void MobileMoney_ChargesOnePointFivePercentRoundedUp(int amount, int expectedFee)
    {
        Assert.Equal(expectedFee, CompanyPayoutFeeCalculator.Calculate(CompanyPayoutMethod.MobileMoney, amount));
    }

    [Fact]
    public void BankTransfer_ChargesFlatOneThousandFrancs()
    {
        Assert.Equal(1_000, CompanyPayoutFeeCalculator.Calculate(CompanyPayoutMethod.BankTransfer, 80_000));
    }

    [Fact]
    public void CashWithdrawal_IsFree()
    {
        Assert.Equal(0, CompanyPayoutFeeCalculator.Calculate(CompanyPayoutMethod.Cash, 80_000));
    }
}
