using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Notifications;
using HomeService.Application.Security;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Companies;

public sealed class CompanyActivationLinkGenerationService(
    IAppDbContext db,
    NotificationDeliveryPreferenceService deliveryPreferences)
{
    public async Task<CompanyActivationLinkGenerationResult> GenerateAsync(
        Guid applicationId,
        string companyPortalBaseUrl,
        int tokenLifetimeHours,
        string changedBy,
        CancellationToken cancellationToken)
        => await GenerateAsync(applicationId, companyPortalBaseUrl, tokenLifetimeHours, changedBy, null, null, cancellationToken);

    public async Task<CompanyActivationLinkGenerationResult> GenerateAsync(
        Guid applicationId,
        string companyPortalBaseUrl,
        int tokenLifetimeHours,
        string changedBy,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await GenerateCoreAsync(
                    applicationId,
                    companyPortalBaseUrl,
                    tokenLifetimeHours,
                    changedBy,
                    actor,
                    auditContext,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts - 1 && db is DbContext context)
            {
                context.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                return CompanyActivationLinkGenerationResult.ConcurrencyConflict(exception.Message);
            }
        }

        return CompanyActivationLinkGenerationResult.ConcurrencyConflict();
    }

    private async Task<CompanyActivationLinkGenerationResult> GenerateCoreAsync(
        Guid applicationId,
        string companyPortalBaseUrl,
        int tokenLifetimeHours,
        string changedBy,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var application = await db.CompanyApplications
            .FirstOrDefaultAsync(application => application.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return CompanyActivationLinkGenerationResult.NotFound();
        }

        if (application.Status is not CompanyApplicationStatus.Approved and not CompanyApplicationStatus.ActivationSent)
        {
            return CompanyActivationLinkGenerationResult.InvalidStatus();
        }

        var previousStatus = application.Status;
        var rawToken = PortalTokenService.GenerateSecureToken();
        var tokenHash = PortalTokenService.HashToken(rawToken);
        var expiresAt = now.AddHours(tokenLifetimeHours);
        var activationLink = CompanyActivationLinkBuilder.Build(companyPortalBaseUrl, application.Id, rawToken);
        var reminderSentAt = application.ActivationEmailSentAt is null ? application.LastReminderSentAt : DateTimeOffset.UtcNow;

        var activeTokens = await db.CompanyActivationTokens
            .Where(token => token.CompanyApplicationId == application.Id
                && token.UsedAt == null
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.Revoke("Remplace par un nouveau token d'activation.");
        }

        var activationToken = application.CreateActivationToken(tokenHash, expiresAt, activationLink, changedBy, revokeExistingTokens: false);
        db.CompanyActivationTokens.Add(activationToken);
        if (previousStatus == CompanyApplicationStatus.ActivationSent)
        {
            application.MarkReminderSent();
        }

        db.CompanyApplicationStatusHistories.Add(new CompanyApplicationStatusHistory(
            application.Id,
            previousStatus,
            CompanyApplicationStatus.ActivationSent,
            "Lien d'activation envoye.",
            changedBy));
        var preference = await deliveryPreferences.GetAsync(
            "CompanyActivationLinkCreated",
            "Company",
            defaultEmailEnabled: true,
            defaultWhatsAppEnabled: true,
            cancellationToken);
        db.NotificationOutboxMessages.AddRange(CompanyActivationLinkNotificationFactory.Create(
            application,
            activationLink,
            expiresAt,
            preference));
        AddAuditLog(
            actor,
            auditContext,
            application,
            previousStatus,
            CompanyApplicationStatus.ActivationSent,
            expiresAt,
            activationLink);

        await db.SaveChangesAsync(cancellationToken);

        return CompanyActivationLinkGenerationResult.Ok(
            new CompanyApplicationActivationLinkResponse(
                application.Id,
                CompanyApplicationStatus.ActivationSent.ToString(),
                application.ActivationEmailSentAt,
                reminderSentAt,
                expiresAt,
                activationLink),
            previousStatus);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CompanyApplication application,
        CompanyApplicationStatus previousStatus,
        CompanyApplicationStatus status,
        DateTimeOffset expiresAt,
        string activationLink)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            "AdminCompanyActivationLinkGenerated",
            nameof(CompanyApplication),
            application.Id,
            "Lien d'activation entreprise genere.",
            auditContext,
            before: new { Status = previousStatus },
            after: new { Status = status, ExpiresAt = expiresAt, ActivationLink = activationLink }));
    }
}
