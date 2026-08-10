using HomeService.Domain.Enums;

namespace HomeService.Application.CompanyPortal;

public static class CompanyPayoutFeeCalculator
{
    public static int Calculate(CompanyPayoutMethod method, int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return method switch
        {
            CompanyPayoutMethod.MobileMoney => (int)Math.Ceiling(amount * 0.015m),
            CompanyPayoutMethod.BankTransfer => 1_000,
            CompanyPayoutMethod.Cash => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
    }
}
