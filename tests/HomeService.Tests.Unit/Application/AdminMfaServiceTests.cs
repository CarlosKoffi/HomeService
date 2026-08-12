using System.Buffers.Binary;
using System.Security.Cryptography;
using HomeService.Application.Abstractions;
using HomeService.Application.Admin;
using HomeService.Application.Security;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Unit.Application;

public sealed class AdminMfaServiceTests
{
    [Fact]
    public async Task Enrollment_ActivatesTotp_AndRecoveryCodeCanOnlyBeUsedOnce()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Finance", "finance@wele.africa");
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        var sut = new AdminMfaService(db, new PlainTextProtector());

        var enrollment = await sut.BeginEnrollmentAsync(admin.Id, CancellationToken.None);
        var activation = await sut.ActivateAsync(
            admin.Id,
            GenerateTotp(enrollment.ManualKey),
            CancellationToken.None);

        Assert.True(activation.Status.IsEnabled);
        Assert.Equal(8, activation.RecoveryCodes.Count);
        var recoveryCode = activation.RecoveryCodes[0];

        var firstUse = await sut.VerifyAsync(admin.Id, recoveryCode, CancellationToken.None);
        var secondUse = await sut.VerifyAsync(admin.Id, recoveryCode, CancellationToken.None);

        Assert.True(firstUse.IsSuccess);
        Assert.True(firstUse.UsedRecoveryCode);
        Assert.False(secondUse.IsSuccess);
    }

    [Fact]
    public async Task FinancialAuthorization_WhenDualApprovalRequired_RequiresTwoDifferentAdmins()
    {
        await using var db = CreateDbContext();
        var first = new AdminUser("Finance 1", "finance1@wele.africa");
        var second = new AdminUser("Finance 2", "finance2@wele.africa");
        db.AdminUsers.AddRange(first, second);
        await db.SaveChangesAsync();
        var mfa = new AdminMfaService(db, new PlainTextProtector());
        var firstActivation = await EnrollAsync(mfa, first.Id);
        var secondActivation = await EnrollAsync(mfa, second.Id);
        var sut = new AdminFinancialAuthorizationService(
            db,
            mfa,
            new AdminFinancialSecurityOptions { DualApprovalThresholdAmount = 100_000, ApprovalValidityMinutes = 15 });
        var resourceId = Guid.NewGuid();

        var firstApproval = await sut.AuthorizeAsync(
            first.Id, "CashPayout", resourceId, "trusted-payload", firstActivation.RecoveryCodes[0],
            150_000, false, CancellationToken.None);
        var repeatedBySameAdmin = await sut.AuthorizeAsync(
            first.Id, "CashPayout", resourceId, "trusted-payload", string.Empty,
            150_000, false, CancellationToken.None);
        var secondApproval = await sut.AuthorizeAsync(
            second.Id, "CashPayout", resourceId, "trusted-payload", secondActivation.RecoveryCodes[0],
            150_000, false, CancellationToken.None);

        Assert.True(firstApproval.AwaitingSecondApproval);
        Assert.True(repeatedBySameAdmin.AwaitingSecondApproval);
        Assert.Equal(1, repeatedBySameAdmin.ApprovalsReceived);
        Assert.True(secondApproval.IsAuthorized);
        Assert.Equal(2, secondApproval.ApprovalsReceived);
    }

    [Fact]
    public async Task Login_WhenMfaIsEnabled_RequiresAndConsumesPersonalSecondFactor()
    {
        await using var db = CreateDbContext();
        var admin = new AdminUser("Finance", "finance-login@wele.africa");
        admin.AcceptInvitation(Sha256PasswordHasher.Hash("Password123"), DateTimeOffset.UtcNow);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        var mfa = new AdminMfaService(db, new PlainTextProtector());
        var activation = await EnrollAsync(mfa, admin.Id);
        var auth = new AdminAuthService(db, mfa);

        var withoutCode = await auth.LoginAsync(
            new AdminLoginRequest(admin.Email, "Password123"),
            CancellationToken.None);
        var withRecoveryCode = await auth.LoginAsync(
            new AdminLoginRequest(admin.Email, "Password123", activation.RecoveryCodes[0]),
            CancellationToken.None);
        var replayedRecoveryCode = await auth.LoginAsync(
            new AdminLoginRequest(admin.Email, "Password123", activation.RecoveryCodes[0]),
            CancellationToken.None);

        Assert.False(withoutCode.IsSuccess);
        Assert.Contains("Authenticator", withoutCode.Message);
        Assert.True(withRecoveryCode.IsSuccess);
        Assert.False(replayedRecoveryCode.IsSuccess);
    }

    private static async Task<HomeService.Contracts.Admin.AdminMfaActivationResponse> EnrollAsync(
        AdminMfaService service,
        Guid adminId)
    {
        var enrollment = await service.BeginEnrollmentAsync(adminId, CancellationToken.None);
        return await service.ActivateAsync(adminId, GenerateTotp(enrollment.ManualKey), CancellationToken.None);
    }

    private static string GenerateTotp(string base32Secret)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in base32Secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                bitsLeft -= 8;
            }
        }

        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        using var hmac = new HMACSHA1(output.ToArray());
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        return (binary % 1_000_000).ToString("D6");
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"admin-mfa-{Guid.NewGuid():N}")
            .Options;
        return new HomeServiceDbContext(options);
    }

    private sealed class PlainTextProtector : IAdminMfaDataProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string protectedValue) => protectedValue;
    }
}
