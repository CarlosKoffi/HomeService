using HomeService.Application.ProviderPortal;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Integration;

public sealed class ProviderInvitationActivationIntegrationTests
{
    [Fact]
    public async Task CompanyInvitation_CanSetPasswordAndLoginWithIt()
    {
        await using var db = CreateDbContext();

        var company = new Company("Entreprise test", "+2250700000000", "contact@example.test");
        company.Approve();
        db.Companies.Add(company);

        var provider = new ProviderProfile(
            company.Id,
            "Awa",
            "Kouame",
            "+2250102030405",
            "awa@example.test",
            new DateOnly(1995, 1, 1),
            "Cocody, Abidjan",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            3,
            5.348m,
            -3.986m,
            10);
        db.Providers.Add(provider);

        var invitation = new ProviderInvitation(
            provider.Id,
            company.Id,
            "WELE-123456",
            "test-token-hash",
            DateTimeOffset.UtcNow.AddDays(1));
        db.ProviderInvitations.Add(invitation);
        await db.SaveChangesAsync();

        var service = new ProviderPortalAuthService(db);
        var activation = await service.ActivateInvitationAsync(
            new ProviderInvitationActivationRequest(
                invitation.Code,
                "testeur12345",
                "testeur12345",
                true),
            CancellationToken.None);

        Assert.True(activation.IsSuccess, activation.ErrorMessage);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var savedProvider = await db.Providers.SingleAsync(item => item.Id == provider.Id);
        var savedInvitation = await db.ProviderInvitations.SingleAsync(item => item.Id == invitation.Id);

        Assert.False(string.IsNullOrWhiteSpace(savedProvider.PasswordHash));
        Assert.Equal(ProviderStatus.Approved, savedProvider.Status);
        Assert.Equal(ProviderInvitationStatus.Accepted, savedInvitation.Status);

        var login = await new ProviderPortalAuthService(db).LoginAsync(
            new ProviderPortalLoginRequest(provider.PhoneNumber, "testeur12345", true),
            CancellationToken.None);

        Assert.True(login.IsSuccess, login.ErrorMessage);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"provider-invitation-activation-{Guid.NewGuid():N}")
            .Options;

        return new HomeServiceDbContext(options);
    }
}
