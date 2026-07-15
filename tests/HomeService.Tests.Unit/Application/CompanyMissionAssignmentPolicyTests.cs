using HomeService.Application.CompanyPortal;

namespace HomeService.Tests.Unit.Application;

public sealed class CompanyMissionAssignmentPolicyTests
{
    [Fact]
    public void Validate_returns_not_found_when_mission_is_missing()
    {
        var result = CompanyMissionAssignmentPolicy.Validate(false, true, true, true, false);

        Assert.False(result.IsValid);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public void Validate_returns_not_found_when_provider_is_not_approved()
    {
        var result = CompanyMissionAssignmentPolicy.Validate(true, true, false, true, false);

        Assert.False(result.IsValid);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public void Validate_rejects_provider_without_mission_service()
    {
        var result = CompanyMissionAssignmentPolicy.Validate(true, true, true, false, false);

        Assert.False(result.IsValid);
        Assert.False(result.IsNotFound);
        Assert.Contains("ne couvre pas", result.Message);
    }

    [Fact]
    public void Validate_rejects_provider_with_blocking_assignment()
    {
        var result = CompanyMissionAssignmentPolicy.Validate(true, true, true, true, true);

        Assert.False(result.IsValid);
        Assert.False(result.IsNotFound);
        Assert.Contains("deja une mission", result.Message);
    }

    [Fact]
    public void Validate_accepts_assignable_provider()
    {
        var result = CompanyMissionAssignmentPolicy.Validate(true, true, true, true, false);

        Assert.True(result.IsValid);
        Assert.False(result.IsNotFound);
        Assert.Null(result.Message);
    }
}
