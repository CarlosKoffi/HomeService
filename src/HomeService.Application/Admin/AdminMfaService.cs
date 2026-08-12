using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Admin;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminMfaService(IAppDbContext db, IAdminMfaDataProtector protector)
{
    private const int EnrollmentMinutes = 10;
    private const int RecoveryCodeCount = 8;
    private const string Issuer = "Wélé Administration";

    public async Task<AdminMfaStatusResponse?> GetStatusAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == adminUserId && item.IsActive, cancellationToken);
        if (admin is null)
        {
            return null;
        }

        var remaining = admin.IsMfaEnabled
            ? await db.AdminMfaRecoveryCodes.CountAsync(
                item => item.AdminUserId == adminUserId && item.UsedAt == null,
                cancellationToken)
            : 0;

        return new AdminMfaStatusResponse(
            admin.IsMfaEnabled,
            admin.MfaEnabledAt,
            remaining,
            !string.IsNullOrWhiteSpace(admin.PendingMfaSecretProtected)
                && admin.PendingMfaExpiresAt > DateTimeOffset.UtcNow,
            admin.PendingMfaExpiresAt);
    }

    public async Task<AdminMfaEnrollmentResponse> BeginEnrollmentAsync(
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(
            item => item.Id == adminUserId && item.IsActive,
            cancellationToken) ?? throw new InvalidOperationException("Administrateur introuvable.");

        var secret = Base32.Encode(RandomNumberGenerator.GetBytes(20));
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(EnrollmentMinutes);
        admin.BeginMfaEnrollment(protector.Protect(secret), expiresAt);
        await db.SaveChangesAsync(cancellationToken);

        var label = Uri.EscapeDataString($"{Issuer}:{admin.Email}");
        var issuer = Uri.EscapeDataString(Issuer);
        var uri = $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
        return new AdminMfaEnrollmentResponse(secret, uri, expiresAt);
    }

    public async Task<AdminMfaActivationResponse> ActivateAsync(
        Guid adminUserId,
        string code,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(
            item => item.Id == adminUserId && item.IsActive,
            cancellationToken) ?? throw new InvalidOperationException("Administrateur introuvable.");

        if (string.IsNullOrWhiteSpace(admin.PendingMfaSecretProtected)
            || !admin.PendingMfaExpiresAt.HasValue
            || admin.PendingMfaExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("La configuration Authenticator a expire. Recommencez l'activation.");
        }

        var secret = protector.Unprotect(admin.PendingMfaSecretProtected);
        if (!Totp.TryValidate(secret, code, DateTimeOffset.UtcNow, null, out var acceptedStep))
        {
            throw new InvalidOperationException("Le code Authenticator est incorrect.");
        }

        var oldCodes = await db.AdminMfaRecoveryCodes
            .Where(item => item.AdminUserId == adminUserId)
            .ToListAsync(cancellationToken);
        db.AdminMfaRecoveryCodes.RemoveRange(oldCodes);

        var recoveryCodes = Enumerable.Range(0, RecoveryCodeCount)
            .Select(_ => GenerateRecoveryCode())
            .ToList();
        foreach (var recoveryCode in recoveryCodes)
        {
            db.AdminMfaRecoveryCodes.Add(new AdminMfaRecoveryCode(adminUserId, HashRecoveryCode(recoveryCode)));
        }

        var now = DateTimeOffset.UtcNow;
        admin.EnableMfa(now);
        admin.RecordMfaVerification(acceptedStep);
        await db.SaveChangesAsync(cancellationToken);

        var status = new AdminMfaStatusResponse(true, now, recoveryCodes.Count, false, null);
        return new AdminMfaActivationResponse(
            status,
            recoveryCodes,
            "Authenticator est active. Conservez les codes de secours hors ligne.");
    }

    public async Task<AdminMfaVerificationResult> VerifyAsync(
        Guid adminUserId,
        string code,
        CancellationToken cancellationToken)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(
            item => item.Id == adminUserId && item.IsActive,
            cancellationToken);
        if (admin is null || !admin.IsMfaEnabled || string.IsNullOrWhiteSpace(admin.MfaSecretProtected))
        {
            return AdminMfaVerificationResult.Fail(
                "Activez Authenticator dans Sécurité avant d'effectuer cette opération.");
        }

        var normalized = NormalizeCode(code);
        if (normalized.Length == 6 && normalized.All(char.IsDigit))
        {
            var secret = protector.Unprotect(admin.MfaSecretProtected);
            if (!Totp.TryValidate(
                    secret,
                    normalized,
                    DateTimeOffset.UtcNow,
                    admin.LastAcceptedMfaTimeStep,
                    out var acceptedStep))
            {
                return AdminMfaVerificationResult.Fail(
                    "Code incorrect ou déjà utilisé. Attendez le prochain code si nécessaire.");
            }

            admin.RecordMfaVerification(acceptedStep);
            await db.SaveChangesAsync(cancellationToken);
            return AdminMfaVerificationResult.Success(false);
        }

        var recoveryHash = HashRecoveryCode(normalized);
        var recovery = await db.AdminMfaRecoveryCodes.FirstOrDefaultAsync(
            item => item.AdminUserId == adminUserId
                && item.CodeHash == recoveryHash
                && item.UsedAt == null,
            cancellationToken);
        if (recovery is null)
        {
            return AdminMfaVerificationResult.Fail("Code Authenticator ou code de secours incorrect.");
        }

        recovery.MarkUsed(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return AdminMfaVerificationResult.Success(true);
    }

    private static string GenerateRecoveryCode()
    {
        var raw = Base32.Encode(RandomNumberGenerator.GetBytes(5));
        return $"{raw[..4]}-{raw[4..8]}";
    }

    private static string HashRecoveryCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeCode(code))));

    private static string NormalizeCode(string value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static class Totp
    {
        public static bool TryValidate(
            string base32Secret,
            string code,
            DateTimeOffset now,
            long? lastAcceptedStep,
            out long acceptedStep)
        {
            acceptedStep = -1;
            var normalized = NormalizeCode(code);
            if (normalized.Length != 6 || !normalized.All(char.IsDigit))
            {
                return false;
            }

            var currentStep = now.ToUnixTimeSeconds() / 30;
            for (var offset = -1; offset <= 1; offset++)
            {
                var candidateStep = currentStep + offset;
                if (lastAcceptedStep.HasValue && candidateStep <= lastAcceptedStep.Value)
                {
                    continue;
                }

                if (Generate(base32Secret, candidateStep) == normalized)
                {
                    acceptedStep = candidateStep;
                    return true;
                }
            }

            return false;
        }

        private static string Generate(string base32Secret, long timeStep)
        {
            var counter = new byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);
            using var hmac = new HMACSHA1(Base32.Decode(base32Secret));
            var hash = hmac.ComputeHash(counter);
            var offset = hash[^1] & 0x0f;
            var binary = ((hash[offset] & 0x7f) << 24)
                | ((hash[offset + 1] & 0xff) << 16)
                | ((hash[offset + 2] & 0xff) << 8)
                | (hash[offset + 3] & 0xff);
            return (binary % 1_000_000).ToString("D6");
        }
    }

    private static class Base32
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string Encode(byte[] bytes)
        {
            var output = new StringBuilder((bytes.Length * 8 + 4) / 5);
            var buffer = 0;
            var bitsLeft = 0;
            foreach (var value in bytes)
            {
                buffer = (buffer << 8) | value;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    output.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                    bitsLeft -= 5;
                }
            }

            if (bitsLeft > 0)
            {
                output.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
            }

            return output.ToString();
        }

        public static byte[] Decode(string value)
        {
            var clean = value.Trim().TrimEnd('=').ToUpperInvariant();
            var output = new List<byte>(clean.Length * 5 / 8);
            var buffer = 0;
            var bitsLeft = 0;
            foreach (var character in clean)
            {
                var index = Alphabet.IndexOf(character);
                if (index < 0)
                {
                    throw new FormatException("Secret Base32 invalide.");
                }

                buffer = (buffer << 5) | index;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xff));
                    bitsLeft -= 8;
                }
            }

            return output.ToArray();
        }
    }
}

public sealed record AdminMfaVerificationResult(bool IsSuccess, bool UsedRecoveryCode, string? Message)
{
    public static AdminMfaVerificationResult Success(bool usedRecoveryCode) => new(true, usedRecoveryCode, null);
    public static AdminMfaVerificationResult Fail(string message) => new(false, false, message);
}
