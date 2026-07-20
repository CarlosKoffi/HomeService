using HomeService.Application.Abstractions;
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
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await GenerateCoreAsync(
                    applicationId,
                    companyPortalBaseUrl,
                    tokenLifetimeHours,
                    changedBy,
                    cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0 && db is DbContext context)
            {
                context.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                return CompanyActivationLinkGenerationResult.ConcurrencyConflict();
            }
        }

        return CompanyActivationLinkGenerationResult.ConcurrencyConflict();
    }

    private async Task<CompanyActivationLinkGenerationResult> GenerateCoreAsync(
        Guid applicationId,
        string companyPortalBaseUrl,
        int tokenLifetimeHours,
        string changedBy,
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

        await db.CompanyActivationTokens
            .Where(token => token.CompanyApplicationId == application.Id
                && token.UsedAt == null
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, "Remplace par un nouveau token d'activation.")
                    .SetProperty(token => token.UpdatedAt, now),
                cancellationToken);

        application.CreateActivationToken(tokenHash, expiresAt, activationLink, changedBy, revokeExistingTokens: false);
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
}
