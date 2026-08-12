using System.Text.Json;
using HomeService.Contracts.Admin;

namespace HomeService.Tests.Unit.Admin;

public sealed class AdminLoginContractSerializationTests
{
    [Fact]
    public void Login_response_with_mfa_fields_can_be_deserialized()
    {
        var adminId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var payload = JsonSerializer.Serialize(new AdminLoginResponse(
            "session-token",
            expiresAt,
            new AdminCurrentUserResponse(
                adminId,
                "Bruce Carl",
                "admin@wele.africa",
                true,
                expiresAt,
                [],
                MfaEnabled: false,
                MfaEnrollmentRequired: true)));

        var response = JsonSerializer.Deserialize<AdminLoginResponse>(payload);

        Assert.NotNull(response);
        Assert.Equal(adminId, response.User.Id);
        Assert.True(response.User.MfaEnrollmentRequired);
        Assert.False(response.User.MfaEnabled);
    }

    [Fact]
    public void Current_user_keeps_backward_compatible_mfa_defaults()
    {
        var response = new AdminCurrentUserResponse(
            Guid.NewGuid(),
            "Awa Kone",
            "awa@wele.africa",
            true,
            DateTimeOffset.UtcNow.AddHours(8),
            []);

        Assert.False(response.MfaEnabled);
        Assert.False(response.MfaEnrollmentRequired);
    }
}
