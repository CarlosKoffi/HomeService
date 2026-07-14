using HomeService.Application.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyEmployeeOperationResultTests
{
    [Fact]
    public void Ok_CarriesProviderSnapshots()
    {
        var provider = CreateProvider();
        var before = new { Status = ProviderStatus.Invited };
        var after = new { Status = ProviderStatus.SuspendedByCompany };

        var result = CompanyEmployeeOperationResult.Ok(provider, before, after);

        Assert.Equal(CompanyEmployeeOperationStatus.Ok, result.Status);
        Assert.Same(provider, result.Provider);
        Assert.Same(before, result.Before);
        Assert.Same(after, result.After);
    }

    [Fact]
    public void NotFound_CarriesMessage()
    {
        var result = CompanyEmployeeOperationResult.NotFound();

        Assert.Equal(CompanyEmployeeOperationStatus.NotFound, result.Status);
        Assert.Equal("Employe introuvable.", result.Message);
        Assert.Null(result.Provider);
    }

    [Fact]
    public void InvitationOk_CarriesProviderAndInvitation()
    {
        var providerId = Guid.NewGuid();
        var invitation = new ProviderInvitation(
            providerId,
            Guid.NewGuid(),
            "KAZA-123456",
            "token-hash",
            DateTimeOffset.UtcNow.AddDays(14));

        var result = CompanyEmployeeInvitationResult.Ok(providerId, invitation);

        Assert.Equal(CompanyEmployeeInvitationStatus.Ok, result.Status);
        Assert.Equal(providerId, result.ProviderId);
        Assert.Same(invitation, result.Invitation);
    }

    [Fact]
    public void InvitationNotFound_CarriesBusinessMessage()
    {
        var result = CompanyEmployeeInvitationResult.NotFound();

        Assert.Equal(CompanyEmployeeInvitationStatus.NotFound, result.Status);
        Assert.Equal("Prestataire introuvable.", result.Message);
        Assert.Null(result.ProviderId);
        Assert.Null(result.Invitation);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Awa",
            "Kone",
            "+2250700000000",
            new DateOnly(1994, 5, 10),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            null,
            null,
            8);
    }
}
