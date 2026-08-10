using HomeService.Domain.Entities;

namespace HomeService.Tests.Unit.Domain;

public sealed class CompanyWalletTests
{
    [Fact]
    public void Funds_MoveThroughPendingAvailableReservedAndWithdrawnBuckets()
    {
        var wallet = new CompanyWallet(Guid.NewGuid());

        wallet.CreditPending(30_000);
        wallet.MakeAvailable(30_000);
        wallet.Reserve(20_000);
        wallet.CompletePayout(20_000);

        Assert.Equal(0, wallet.PendingBalance);
        Assert.Equal(10_000, wallet.AvailableBalance);
        Assert.Equal(0, wallet.ReservedBalance);
        Assert.Equal(20_000, wallet.WithdrawnBalance);
        Assert.Equal(4, wallet.Version);
    }

    [Fact]
    public void FailedPayout_ReturnsReservationToAvailableBalance()
    {
        var wallet = new CompanyWallet(Guid.NewGuid());
        wallet.CreditPending(15_000);
        wallet.MakeAvailable(15_000);
        wallet.Reserve(15_000);

        wallet.ReleaseReservation(15_000);

        Assert.Equal(15_000, wallet.AvailableBalance);
        Assert.Equal(0, wallet.ReservedBalance);
        Assert.Equal(0, wallet.WithdrawnBalance);
    }

    [Fact]
    public void CannotReserveMoreThanAvailableBalance()
    {
        var wallet = new CompanyWallet(Guid.NewGuid());
        wallet.CreditPending(8_000);

        Assert.Throws<InvalidOperationException>(() => wallet.Reserve(8_000));
    }
}
