using HomeService.Application.CompanyPortal;
using HomeService.Application.Security;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPortalAuthServiceTests
{
    [Theory]
    [InlineData("", "Password123")]
    [InlineData("direction@entreprise.ci", "")]
    [InlineData("   ", "   ")]
    public async Task LoginAsync_WhenCredentialsMissing_ReturnsMissingCredentials(string email, string password)
    {
        var service = new CompanyPortalAuthService(null!);

        var result = await service.LoginAsync(new CompanyPortalLoginRequest(email, password, false), CancellationToken.None);

        Assert.Equal(CompanyPortalLoginStatus.MissingCredentials, result.Status);
        Assert.Equal("Email et mot de passe sont obligatoires.", result.Message);
    }

    [Fact]
    public async Task GetAuthenticatedCompanyIdAsync_WithActiveBearerSession_ReturnsOwningCompany()
    {
        await using var db = CreateDbContext();
        var company = new Company("Entreprise Test", "+2250102030405", "direction@test.ci");
        company.Approve();
        var user = new CompanyPortalUser(company.Id, "Awa Kone", "direction@test.ci", "hash", true);
        const string token = "company-session-token";
        db.AddRange(company, user, new CompanyPortalSession(
            user.Id,
            PortalTokenService.HashToken(token),
            DateTimeOffset.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        var service = new CompanyPortalAuthService(db);
        var companyId = await service.GetAuthenticatedCompanyIdAsync($"Bearer {token}", CancellationToken.None);

        Assert.Equal(company.Id, companyId);
    }

    [Fact]
    public async Task GetAuthenticatedCompanyIdAsync_WithInvalidToken_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = new CompanyPortalAuthService(db);

        var companyId = await service.GetAuthenticatedCompanyIdAsync("Bearer unknown", CancellationToken.None);

        Assert.Null(companyId);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HomeServiceDbContext(options);
    }
}
