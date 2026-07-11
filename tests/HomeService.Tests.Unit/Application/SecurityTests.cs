using HomeService.Application.Companies;
using HomeService.Application.Security;

namespace HomeService.Tests.Unit.Application;

public sealed class SecurityTests
{
    [Fact]
    public void GenerateSecureToken_ReturnsUrlSafeToken()
    {
        var token = PortalTokenService.GenerateSecureToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void HashToken_ReturnsDeterministicSha256Hash()
    {
        var hashA = PortalTokenService.HashToken("token");
        var hashB = PortalTokenService.HashToken("token");
        var hashC = PortalTokenService.HashToken("other-token");

        Assert.Equal(hashA, hashB);
        Assert.NotEqual(hashA, hashC);
        Assert.Equal(64, hashA.Length);
    }

    [Fact]
    public void PasswordHash_VerifiesOriginalPasswordOnly()
    {
        var hash = Sha256PasswordHasher.Hash("Password123");

        Assert.True(Sha256PasswordHasher.Verify("Password123", hash));
        Assert.False(Sha256PasswordHasher.Verify("Password124", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad-format")]
    [InlineData("sha256:only-two-parts")]
    public void PasswordVerify_WhenHashFormatIsInvalid_ReturnsFalse(string passwordHash)
    {
        Assert.False(Sha256PasswordHasher.Verify("Password123", passwordHash));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("720", 720)]
    [InlineData("0", 72)]
    [InlineData("721", 72)]
    [InlineData("not-a-number", 72)]
    [InlineData(null, 72)]
    public void ResolveActivationTokenHours_EnforcesBounds(string? value, int expected)
    {
        Assert.Equal(expected, CompanyActivationTokenLifetimeResolver.ResolveHours(value));
    }

    [Fact]
    public void BuildActivationLink_TrimsBaseUrlAndEncodesToken()
    {
        var applicationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var link = CompanyActivationLinkBuilder.Build(" https://company.kaza.ci/ ", applicationId, "token+/=");

        Assert.Equal("https://company.kaza.ci/activate-company/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?token=token%2B%2F%3D", link);
    }
}
