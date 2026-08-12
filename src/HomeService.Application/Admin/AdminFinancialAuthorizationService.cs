using System.Security.Cryptography;
using System.Text;
using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminFinancialAuthorizationService(
    IAppDbContext db,
    AdminMfaService mfaService,
    AdminFinancialSecurityOptions options)
{
    public async Task<AdminFinancialAuthorizationResult> AuthorizeAsync(
        Guid adminUserId,
        string operation,
        Guid resourceId,
        string trustedPayload,
        string mfaCode,
        int amount,
        bool forceDualApproval,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trustedPayload)));
        var approvalsRequired = forceDualApproval || amount >= options.DualApprovalThresholdAmount ? 2 : 1;
        var activeApprovals = await db.AdminFinancialApprovals
            .Where(item => item.Operation == operation
                && item.ResourceId == resourceId
                && item.PayloadHash == payloadHash
                && item.CompletedAt == null
                && item.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        var existing = activeApprovals.FirstOrDefault(item => item.AdminUserId == adminUserId);
        if (existing is null)
        {
            var verification = await mfaService.VerifyAsync(adminUserId, mfaCode, cancellationToken);
            if (!verification.IsSuccess)
            {
                return AdminFinancialAuthorizationResult.Fail(
                    verification.Message ?? "Confirmation Authenticator refusée.",
                    approvalsRequired,
                    activeApprovals.Select(item => item.AdminUserId).Distinct().Count());
            }

            existing = new AdminFinancialApproval(
                adminUserId,
                operation,
                resourceId,
                payloadHash,
                now,
                now.AddMinutes(options.ApprovalValidityMinutes));
            db.AdminFinancialApprovals.Add(existing);
            await db.SaveChangesAsync(cancellationToken);
            activeApprovals.Add(existing);
        }

        var approvalsReceived = activeApprovals.Select(item => item.AdminUserId).Distinct().Count();
        if (approvalsReceived < approvalsRequired)
        {
            return AdminFinancialAuthorizationResult.Pending(
                payloadHash,
                approvalsRequired,
                approvalsReceived,
                "Première validation enregistrée. Un autre administrateur financier doit confirmer avec son propre Authenticator.");
        }

        return AdminFinancialAuthorizationResult.Authorized(payloadHash, approvalsRequired, approvalsReceived);
    }

    public async Task MarkCompletedAsync(
        string operation,
        Guid resourceId,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var approvals = await db.AdminFinancialApprovals
            .Where(item => item.Operation == operation
                && item.ResourceId == resourceId
                && item.PayloadHash == payloadHash
                && item.CompletedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var approval in approvals)
        {
            approval.MarkCompleted(now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AdminFinancialSecurityOptions
{
    public int DualApprovalThresholdAmount { get; init; } = 100_000;
    public int ApprovalValidityMinutes { get; init; } = 15;
}

public sealed record AdminFinancialAuthorizationResult(
    bool IsAuthorized,
    bool AwaitingSecondApproval,
    string? PayloadHash,
    int ApprovalsRequired,
    int ApprovalsReceived,
    string? Message)
{
    public static AdminFinancialAuthorizationResult Authorized(string payloadHash, int required, int received) =>
        new(true, false, payloadHash, required, received, null);

    public static AdminFinancialAuthorizationResult Pending(string payloadHash, int required, int received, string message) =>
        new(false, true, payloadHash, required, received, message);

    public static AdminFinancialAuthorizationResult Fail(string message, int required, int received) =>
        new(false, false, null, required, received, message);
}
