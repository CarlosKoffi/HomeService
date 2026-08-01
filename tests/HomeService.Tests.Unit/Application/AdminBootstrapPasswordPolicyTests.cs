using HomeService.Application.Admin;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminBootstrapPasswordPolicyTests
{
    [Fact]
    public void ShouldSetPassword_WhenAccountHasNoPassword_ReturnsTrue()
    {
        Assert.True(AdminBootstrapPasswordPolicy.ShouldSetPassword(null, forcePasswordReset: false));
    }

    [Fact]
    public void ShouldSetPassword_WhenAccountAlreadyHasPassword_ReturnsFalse()
    {
        Assert.False(AdminBootstrapPasswordPolicy.ShouldSetPassword("existing-hash", forcePasswordReset: false));
    }

    [Fact]
    public void ShouldSetPassword_WhenResetIsExplicitlyForced_ReturnsTrue()
    {
        Assert.True(AdminBootstrapPasswordPolicy.ShouldSetPassword("existing-hash", forcePasswordReset: true));
    }
}
