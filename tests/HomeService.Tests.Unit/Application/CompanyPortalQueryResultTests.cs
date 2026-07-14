using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyPortalQueryResultTests
{
    [Fact]
    public void ProfileNotFound_ReturnsBusinessMessage()
    {
        var result = CompanyPortalProfileResult.NotFound();

        Assert.False(result.IsSuccess);
        Assert.Equal("Entreprise introuvable ou inactive.", result.Message);
    }

    [Fact]
    public void MissionsOk_CarriesMissionList()
    {
        var missions = Array.Empty<CompanyPortalMissionResponse>();

        var result = CompanyPortalMissionsResult.Ok(missions);

        Assert.True(result.IsSuccess);
        Assert.Same(missions, result.Missions);
    }

    [Fact]
    public void PaymentsOk_CarriesSummary()
    {
        var summary = new CompanyPortalPaymentSummaryResponse("month", 0, 0, 0, 0, 0, "XOF", []);

        var result = CompanyPortalPaymentsResult.Ok(summary);

        Assert.True(result.IsSuccess);
        Assert.Same(summary, result.Summary);
    }
}
