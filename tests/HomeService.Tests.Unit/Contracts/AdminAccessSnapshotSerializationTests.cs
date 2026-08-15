using System.Text.Json;
using HomeService.Contracts.Admin;

namespace HomeService.Tests.Unit.Contracts;

public sealed class AdminAccessSnapshotSerializationTests
{
    [Fact]
    public void Access_snapshot_deserializes_admin_users_with_mfa_fields()
    {
        var adminId = Guid.NewGuid();
        var json = $$"""
        {
          "roles": [],
          "modules": [],
          "admins": [
            {
              "id": "{{adminId}}",
              "fullName": "Admin Wélé",
              "email": "admin@wele.africa",
              "isSuperAdmin": true,
              "isActive": true,
              "hasActivatedAccess": true,
              "invitationExpiresAt": null,
              "lastLoginAt": "2026-08-14T08:00:00+00:00",
              "roles": ["Super administrateur"],
              "mfaEnabled": true,
              "mfaEnrollmentRequired": false
            }
          ]
        }
        """;

        var snapshot = JsonSerializer.Deserialize<AdminAccessSnapshotResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var admin = Assert.Single(Assert.IsType<AdminAccessSnapshotResponse>(snapshot).Admins);
        Assert.Equal(adminId, admin.Id);
        Assert.True(admin.MfaEnabled);
        Assert.False(admin.MfaEnrollmentRequired);
        Assert.Equal("Super administrateur", Assert.Single(admin.Roles));
    }
}
