using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Companies;

public sealed class CompanyActivationLinkGenerationService(IAppDbContext db)
{
    public async Task<CompanyActivationLinkGenerationResult> GenerateAsync(
        Guid applicationId,
        string companyPortalBaseUrl,
        int tokenLifetimeHours,
        string changedBy,
        CancellationToken cancellationToken)
    {
        var application = await db.CompanyApplications
            .Include(application => application.ActivationTokens)
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
        var expiresAt = DateTimeOffset.UtcNow.AddHours(tokenLifetimeHours);
        var activationLink = CompanyActivationLinkBuilder.Build(companyPortalBaseUrl, application.Id, rawToken);
        var reminderSentAt = application.ActivationEmailSentAt is null ? application.LastReminderSentAt : DateTimeOffset.UtcNow;

        application.CreateActivationToken(tokenHash, expiresAt, activationLink, changedBy);
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
        db.NotificationOutboxMessages.AddRange(CompanyActivationLinkNotificationFactory.Create(application, activationLink, expiresAt));

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
