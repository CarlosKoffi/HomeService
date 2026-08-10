using HomeService.Application.Abstractions;
using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyWalletServiceTests
{
    [Fact]
    public async Task SaveDestinationAsync_ReplacesTheOnlyActiveDestination()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise test", "+2250700000000", "test@example.com");
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var service = new CompanyWalletService(db, new TestProtector(), new DisabledPayoutGateway());

        var first = await service.SaveDestinationAsync(company.Id, new(
            "Cash", "Retrait en agence", "Awa Kouame", "agency", string.Empty), CancellationToken.None);
        var second = await service.SaveDestinationAsync(company.Id, new(
            "MobileMoney", "Mobile Money - Wave", "Awa Kouame", "wave", "+2250700000000"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Single(second.Response!.Destinations);
        Assert.Equal("MobileMoney", second.Response.Destinations[0].Method);
        Assert.Equal(2, await db.CompanyPayoutDestinations.CountAsync());
        Assert.Single(await db.CompanyPayoutDestinations.Where(item => item.IsActive).ToListAsync());
        Assert.Single(await db.CompanyPayoutDestinations.Where(item => !item.IsActive).ToListAsync());
    }

    [Fact]
    public async Task CompleteCashPayoutAsync_MovesReservedFundsToWithdrawnAndStoresProof()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise test", "+2250700000000", "test@example.com");
        var wallet = new CompanyWallet(company.Id);
        wallet.CreditPending(10_000);
        wallet.MakeAvailable(10_000);
        db.Companies.Add(company);
        db.CompanyWallets.Add(wallet);
        await db.SaveChangesAsync();
        var service = new CompanyWalletService(db, new TestProtector(), new DisabledPayoutGateway());
        var destinationResult = await service.SaveDestinationAsync(company.Id, new(
            "Cash", "Retrait en agence", "Awa Kouame", "agency", string.Empty), CancellationToken.None);
        var destinationId = Assert.Single(destinationResult.Response!.Destinations).Id;
        Assert.True(await service.VerifyDestinationAsync(destinationId, null, CancellationToken.None));

        var requestResult = await service.RequestPayoutAsync(
            company.Id,
            new(destinationId, 6_000),
            CancellationToken.None);
        var payoutId = Assert.Single(requestResult.Response!.Payouts).Id;
        Assert.True(await service.CompleteCashPayoutAsync(payoutId, "RECU-2026-0001", CancellationToken.None));

        var updatedWallet = await db.CompanyWallets.SingleAsync(item => item.CompanyId == company.Id);
        var payout = await db.CompanyPayoutRequests.SingleAsync(item => item.Id == payoutId);
        Assert.Equal(4_000, updatedWallet.AvailableBalance);
        Assert.Equal(0, updatedWallet.ReservedBalance);
        Assert.Equal(6_000, updatedWallet.WithdrawnBalance);
        Assert.Equal("Paid", payout.Status.ToString());
        Assert.Equal("RECU-2026-0001", payout.ProofReference);
        var entries = await db.CompanyWalletEntries.Where(item => item.PayoutRequestId == payoutId).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, item => item.Type.ToString() == "PayoutPaid");
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"company-wallet-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }

    private sealed class TestProtector : IPayoutDataProtector
    {
        public string Protect(string value) => $"protected:{value}";
        public string Unprotect(string protectedValue) => protectedValue["protected:".Length..];
    }

    private sealed class DisabledPayoutGateway : ICompanyPayoutGateway
    {
        public bool IsEnabled => false;

        public Task<CompanyPayoutGatewayResult> CreateAsync(
            CompanyPayoutGatewayRequest request,
            CancellationToken cancellationToken) => Task.FromResult(CompanyPayoutGatewayResult.Disabled());

        public Task<CompanyPayoutGatewayResult> GetStatusAsync(
            string externalTransactionId,
            CancellationToken cancellationToken) => Task.FromResult(CompanyPayoutGatewayResult.Disabled());
    }
}
