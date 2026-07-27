using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Clients;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Application.ProviderPortal;
using HomeService.Contracts.Clients;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Tests.Integration;

public sealed class ClientMissionWorkflowIntegrationTests
{
    [Fact]
    public async Task ClientMission_CanMoveFromRequestToCustomerValidatedCompletion_WithMockedPaymentAndNotifications()
    {
        await using var db = CreateDbContext();
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);

        var creation = await CreateClientRequestService(db).CreateAsync(
            CreateMissionRequest(seed.Service.Id, seed.Prestation.Id),
            CancellationToken.None);

        Assert.True(creation.IsSuccess);
        Assert.NotNull(creation.Response);
        Assert.Equal(1, creation.Response.CandidateCompanyCount);
        Assert.NotEmpty(creation.Response.MissionNumber);
        Assert.Single(await db.MissionAttachments.ToListAsync());

        var offerService = new CompanyMissionOfferService(db);
        var openOffers = await offerService.ListOpenOffersAsync(seed.Company.Id, CancellationToken.None);
        Assert.True(openOffers.IsSuccess);
        Assert.Single(openOffers.Offers);

        var acceptedOffer = await offerService.AcceptAsync(seed.Company.Id, openOffers.Offers[0].OfferId, CancellationToken.None);
        Assert.True(acceptedOffer.IsSuccess);
        Assert.NotNull(acceptedOffer.Response);

        var assignmentService = CreateAssignmentService(db);
        var assignableProviders = await assignmentService.ListAssignableProvidersAsync(
            seed.Company.Id,
            acceptedOffer.Response.MissionId,
            CancellationToken.None);
        Assert.True(assignableProviders.IsSuccess);
        Assert.Contains(assignableProviders.Providers, provider => provider.Id == seed.Provider.Id);

        var assignmentResult = await assignmentService.AssignAsync(
            seed.Company.Id,
            acceptedOffer.Response.MissionId,
            seed.Provider.Id,
            quotedAmount: 12000,
            overMaxJustification: null,
            CancellationToken.None);
        Assert.True(assignmentResult.IsSuccess);
        Assert.NotNull(assignmentResult.Response);

        var providerPushAfterAssignment = await db.NotificationOutboxMessages
            .SingleAsync(message => message.RelatedEntityId == assignmentResult.Response.AssignmentId);
        Assert.Equal(NotificationChannel.MobilePush, providerPushAfterAssignment.Channel);
        Assert.Equal(NotificationStatus.Pending, providerPushAfterAssignment.Status);

        var workflow = new ProviderMissionWorkflowService();
        var assignment = await LoadAssignmentAsync(db, assignmentResult.Response.AssignmentId);
        var provider = await LoadProviderAsync(db, seed.Provider.Id);
        var acceptResult = workflow.AcceptMission(
            provider,
            assignment,
            new ProviderAcceptMissionRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, acceptResult.Status);
        await db.SaveChangesAsync();

        var confirmation = await CreateClientConfirmationService(db).ConfirmAsync(
            acceptedOffer.Response.MissionId,
            new ConfirmClientMissionRequest(ClientPhoneNumber, "MM-MOCK-001"),
            CancellationToken.None);
        Assert.True(confirmation.IsSuccess);
        Assert.NotNull(confirmation.Response);
        Assert.Equal(1800, confirmation.Response.PlatformCommissionAmount);
        Assert.Equal(10200, confirmation.Response.CompanyPayoutAmount);
        Assert.True(confirmation.Response.ContactDetailsReleased);

        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == seed.Company.Id
            && notification.Type == "MissionQuoteAcceptedByCustomer");

        assignment = await LoadAssignmentAsync(db, assignmentResult.Response.AssignmentId);
        provider = await LoadProviderAsync(db, seed.Provider.Id);
        var startResult = workflow.StartMission(
            provider,
            assignment,
            new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, startResult.Status);

        var completeResult = workflow.CompleteMission(
            provider,
            assignment,
            new ProviderCompleteMissionRequest(90, "Intervention terminee proprement.", "missions/MIS/photo-fin.jpg"));
        Assert.Equal(ProviderMissionOperationStatus.Ok, completeResult.Status);
        await db.SaveChangesAsync();

        var validation = await CreateCompletionValidationService(db).ValidateAsync(
            acceptedOffer.Response.MissionId,
            new ValidateClientMissionCompletionRequest(ClientPhoneNumber, 5, 5, 4, 5, "Tres bon service.", "PAYOUT-MOCK-001"),
            CancellationToken.None);
        Assert.True(validation.IsSuccess);
        Assert.NotNull(validation.Response);
        Assert.Equal("Paid", validation.Response.PaymentStatus);
        Assert.Equal(5, validation.Response.OverallRating);
        Assert.Equal(10200, validation.Response.CompanyPayoutAmount);

        var mission = await db.Missions.SingleAsync(mission => mission.Id == acceptedOffer.Response.MissionId);
        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Equal(PaymentStatus.Paid, mission.PaymentStatus);
        Assert.NotNull(mission.CompanyPayoutReleasedAt);

        Assert.Equal(3, await db.NotificationOutboxMessages.CountAsync(message => message.Channel == NotificationChannel.MobilePush));
    }

    [Fact]
    public async Task ClientMissionCancellation_AfterClientConfirmation_KeepsCancellationFeeAndTracksRefundFinancials()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateConfirmedMissionScenarioAsync(db);

        var result = await new ClientMissionCancellationService(db).CancelAsync(
            scenario.MissionId,
            new CancelClientMissionRequest(ClientPhoneNumber, "CustomerNoShow", "Client indisponible."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("Cancelled", result.Response.Status);
        Assert.Equal(2500, result.Response.CancellationFeeAmount);
        Assert.Equal(9500, result.Response.RefundAmount);
        Assert.Equal("Refunded", result.Response.PaymentStatus);

        var mission = await db.Missions.SingleAsync(mission => mission.Id == scenario.MissionId);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal(MissionCancellationActor.Customer, mission.CancelledBy);

        var financialLines = await db.MissionFinancialBreakdowns
            .Where(line => line.MissionId == scenario.MissionId)
            .ToListAsync();
        Assert.Contains(financialLines, line => line.LineType == MissionFinancialLineType.CancellationFee && line.Amount == 2500);
        Assert.Contains(financialLines, line => line.LineType == MissionFinancialLineType.Refund && line.Amount == -9500);

        var cancellationMilestone = await db.MissionPaymentMilestones
            .SingleAsync(milestone => milestone.MissionId == scenario.MissionId
                && milestone.Trigger == MissionPaymentMilestoneTrigger.Cancellation);
        Assert.Equal(2500, cancellationMilestone.Amount);
        Assert.Equal(MissionPaymentMilestoneStatus.Pending, cancellationMilestone.Status);

        Assert.Contains(await db.CompanyPortalActivities.ToListAsync(), activity =>
            activity.CompanyId == scenario.CompanyId
            && activity.Title == "Mission annulee par le client");
    }

    [Fact]
    public async Task AdminDispute_AfterCompletedMission_CanApprovePartialRefundAndNotifyCustomer()
    {
        await using var db = CreateDbContext();
        var completedMission = await CreateCustomerValidatedMissionScenarioAsync(db);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            completedMission.CustomerId,
            MobileDevicePlatform.Android,
            "customer-device-token",
            "Telephone client"));
        await db.SaveChangesAsync();

        var disputeService = CreateDisputeService(db);
        var openResult = await disputeService.OpenAsync(
            completedMission.MissionId,
            "Other",
            "Le client conteste une partie de la prestation.",
            AuditActor.Admin(),
            null,
            CancellationToken.None);
        Assert.Equal(AdminMissionOperationStatus.Ok, openResult.Status);

        var resolveResult = await disputeService.ResolveAsync(
            completedMission.MissionId,
            "PartialRefund",
            "Remboursement partiel valide par le support.",
            refundPercent: 25,
            refundAmount: null,
            AuditActor.Admin(),
            null,
            CancellationToken.None);
        Assert.Equal(AdminMissionOperationStatus.Ok, resolveResult.Status);

        var mission = await db.Missions.SingleAsync(mission => mission.Id == completedMission.MissionId);
        Assert.Equal(MissionStatus.Resolved, mission.Status);
        Assert.Equal(PaymentStatus.Refunded, mission.PaymentStatus);
        Assert.Equal(3000, mission.RefundAmount);

        var dispute = await db.MissionDisputes.SingleAsync(dispute => dispute.MissionId == completedMission.MissionId);
        Assert.Equal(MissionDisputeStatus.Resolved, dispute.Status);
        Assert.Equal(MissionDisputeResolution.PartialRefund, dispute.Resolution);
        Assert.Equal(2500, dispute.RefundPercentBasisPoints);
        Assert.Equal(3000, dispute.RefundAmount);

        Assert.Contains(await db.MissionFinancialBreakdowns.ToListAsync(), line =>
            line.MissionId == completedMission.MissionId
            && line.LineType == MissionFinancialLineType.Refund
            && line.Amount == -3000);

        var customerNotifications = await db.NotificationOutboxMessages
            .Where(message => message.RelatedEntityId == completedMission.MissionId)
            .ToListAsync();
        Assert.Contains(customerNotifications, message => message.Channel == NotificationChannel.MobilePush);
        Assert.Contains(customerNotifications, message => message.Channel == NotificationChannel.WhatsApp);
        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == completedMission.CompanyId
            && notification.Type == "MissionDisputeResolved");
    }

    [Fact]
    public async Task ProviderRefusal_RemovesProviderFromSameMissionAndAllowsAnotherProviderAssignment()
    {
        await using var db = CreateDbContext();
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);
        var secondProvider = CreateProvider(seed.Company.Id, seed.Service.Id, "Mamadou", "Diallo", "+225 0555000011");
        db.Providers.Add(secondProvider);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            secondProvider.Id,
            MobileDevicePlatform.Android,
            "second-provider-device-token",
            "Android terrain 2"));
        await db.SaveChangesAsync();

        var creation = await CreateClientRequestService(db).CreateAsync(
            CreateMissionRequest(seed.Service.Id, seed.Prestation.Id),
            CancellationToken.None);
        Assert.True(creation.IsSuccess);

        var offerService = new CompanyMissionOfferService(db);
        var offers = await offerService.ListOpenOffersAsync(seed.Company.Id, CancellationToken.None);
        var acceptedOffer = await offerService.AcceptAsync(seed.Company.Id, offers.Offers[0].OfferId, CancellationToken.None);
        Assert.True(acceptedOffer.IsSuccess);

        var assignmentService = CreateAssignmentService(db);
        var firstAssignment = await assignmentService.AssignAsync(
            seed.Company.Id,
            acceptedOffer.Response!.MissionId,
            seed.Provider.Id,
            quotedAmount: 12000,
            overMaxJustification: null,
            CancellationToken.None);
        Assert.True(firstAssignment.IsSuccess);

        var workflow = new ProviderMissionWorkflowService();
        var refusedAssignment = await LoadAssignmentAsync(db, firstAssignment.Response!.AssignmentId);
        var refusingProvider = await LoadProviderAsync(db, seed.Provider.Id);
        var refusal = workflow.RefuseMission(
            refusingProvider,
            refusedAssignment,
            new ProviderRefuseMissionRequest(nameof(ProviderMissionRefusalReason.Unavailable), "Plus disponible."));
        Assert.Equal(ProviderMissionOperationStatus.Ok, refusal.Status);
        await db.SaveChangesAsync();

        var assignableAfterRefusal = await assignmentService.ListAssignableProvidersAsync(
            seed.Company.Id,
            acceptedOffer.Response.MissionId,
            CancellationToken.None);
        Assert.True(assignableAfterRefusal.IsSuccess);
        Assert.DoesNotContain(assignableAfterRefusal.Providers, provider => provider.Id == seed.Provider.Id);
        Assert.Contains(assignableAfterRefusal.Providers, provider => provider.Id == secondProvider.Id);

        var retrySameProvider = await assignmentService.AssignAsync(
            seed.Company.Id,
            acceptedOffer.Response.MissionId,
            seed.Provider.Id,
            quotedAmount: 12000,
            overMaxJustification: null,
            CancellationToken.None);
        Assert.False(retrySameProvider.IsSuccess);

        var secondAssignment = await assignmentService.AssignAsync(
            seed.Company.Id,
            acceptedOffer.Response.MissionId,
            secondProvider.Id,
            quotedAmount: 12500,
            overMaxJustification: null,
            CancellationToken.None);
        Assert.True(secondAssignment.IsSuccess);

        var mission = await db.Missions.SingleAsync(mission => mission.Id == acceptedOffer.Response.MissionId);
        Assert.Equal(secondProvider.Id, mission.ProviderId);
        Assert.Equal(MissionStatus.Assigned, mission.Status);
        Assert.Equal(2, await db.ProviderMissionAssignments.CountAsync(assignment => assignment.MissionId == mission.Id));
        Assert.Contains(await db.ProviderMissionAssignments.ToListAsync(), assignment =>
            assignment.ProviderId == seed.Provider.Id
            && assignment.Status == ProviderMissionAssignmentStatus.Refused);
    }

    private static async Task<ConfirmedMissionScenario> CreateConfirmedMissionScenarioAsync(HomeServiceDbContext db)
    {
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);
        var creation = await CreateClientRequestService(db).CreateAsync(
            CreateMissionRequest(seed.Service.Id, seed.Prestation.Id),
            CancellationToken.None);
        Assert.True(creation.IsSuccess);

        var offerService = new CompanyMissionOfferService(db);
        var offers = await offerService.ListOpenOffersAsync(seed.Company.Id, CancellationToken.None);
        var offer = await offerService.AcceptAsync(seed.Company.Id, offers.Offers[0].OfferId, CancellationToken.None);
        Assert.True(offer.IsSuccess);

        var assignment = await CreateAssignmentService(db).AssignAsync(
            seed.Company.Id,
            offer.Response!.MissionId,
            seed.Provider.Id,
            quotedAmount: 12000,
            overMaxJustification: null,
            CancellationToken.None);
        Assert.True(assignment.IsSuccess);

        var loadedAssignment = await LoadAssignmentAsync(db, assignment.Response!.AssignmentId);
        var loadedProvider = await LoadProviderAsync(db, seed.Provider.Id);
        var acceptResult = new ProviderMissionWorkflowService().AcceptMission(
            loadedProvider,
            loadedAssignment,
            new ProviderAcceptMissionRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, acceptResult.Status);
        await db.SaveChangesAsync();

        var confirmation = await CreateClientConfirmationService(db).ConfirmAsync(
            offer.Response.MissionId,
            new ConfirmClientMissionRequest(ClientPhoneNumber, "MM-MOCK-CANCEL"),
            CancellationToken.None);
        Assert.True(confirmation.IsSuccess);

        return new ConfirmedMissionScenario(offer.Response.MissionId, seed.Company.Id);
    }

    private static async Task<CustomerValidatedMissionScenario> CreateCustomerValidatedMissionScenarioAsync(HomeServiceDbContext db)
    {
        var scenario = await CreateConfirmedMissionScenarioAsync(db);
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .SingleAsync(item => item.MissionId == scenario.MissionId);
        var provider = await LoadProviderAsync(db, assignment.ProviderId);
        var workflow = new ProviderMissionWorkflowService();

        var startResult = workflow.StartMission(
            provider,
            assignment,
            new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, startResult.Status);

        var completeResult = workflow.CompleteMission(
            provider,
            assignment,
            new ProviderCompleteMissionRequest(90, "Mission terminee.", "missions/MIS/photo-fin.jpg"));
        Assert.Equal(ProviderMissionOperationStatus.Ok, completeResult.Status);
        await db.SaveChangesAsync();

        var validation = await CreateCompletionValidationService(db).ValidateAsync(
            scenario.MissionId,
            new ValidateClientMissionCompletionRequest(ClientPhoneNumber, 5, 4, 5, 4, "Service valide.", "PAYOUT-MOCK-DISPUTE"),
            CancellationToken.None);
        Assert.True(validation.IsSuccess);

        var mission = await db.Missions.AsNoTracking().SingleAsync(item => item.Id == scenario.MissionId);
        return new CustomerValidatedMissionScenario(scenario.MissionId, scenario.CompanyId, mission.CustomerId);
    }

    private static ClientMissionRequestService CreateClientRequestService(HomeServiceDbContext db)
    {
        return new ClientMissionRequestService(
            db,
            new MissionDispatchService(db, new MissionDispatchScoringService()));
    }

    private static CompanyMissionAssignmentService CreateAssignmentService(HomeServiceDbContext db)
    {
        return new CompanyMissionAssignmentService(
            db,
            new MobilePushNotificationQueueService(db),
            new NotificationDeliveryPreferenceService(db),
            new NotificationTemplateService(db));
    }

    private static ClientMissionConfirmationService CreateClientConfirmationService(HomeServiceDbContext db)
    {
        return new ClientMissionConfirmationService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private static ClientMissionCompletionValidationService CreateCompletionValidationService(HomeServiceDbContext db)
    {
        return new ClientMissionCompletionValidationService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private static AdminMissionDisputeService CreateDisputeService(HomeServiceDbContext db)
    {
        return new AdminMissionDisputeService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db),
            new NotificationDeliveryPreferenceService(db),
            new NotificationTemplateService(db));
    }

    private static async Task<ProviderMissionAssignment> LoadAssignmentAsync(HomeServiceDbContext db, Guid assignmentId)
    {
        return await db.ProviderMissionAssignments
            .Include(assignment => assignment.Mission)
            .SingleAsync(assignment => assignment.Id == assignmentId);
    }

    private static async Task<ProviderProfile> LoadProviderAsync(HomeServiceDbContext db, Guid providerId)
    {
        return await db.Providers
            .Include(provider => provider.Company)
            .SingleAsync(provider => provider.Id == providerId);
    }

    private static HomeServiceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomeServiceDbContext>()
            .UseInMemoryDatabase($"homeservice-workflow-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            .Options;

        return new HomeServiceDbContext(options);
    }

    private static async Task<WorkflowSeed> SeedApprovedCompanyProviderAndServiceAsync(HomeServiceDbContext db)
    {
        var service = new Service("Menage a domicile", "Nettoyage residentiel", null);
        service.UpdatePriceRange(8000, 15000, "XOF");
        service.Approve();
        var prestation = service.AddPrestation("Nettoyage residentiel", "Appartement et maison", 1, 8000, 15000, "XOF");

        var company = new Company("CI Home Service", "+225 0700000001", "ops@ci-home.test");
        company.UpdateCompanyInformation("CI Home Service", "SARL", "RCCM-CI-ABJ-001", "DFE-001", "Abidjan", "Cocody Angre");
        company.UpdateOperations("Cocody, Angre, Abidjan", "Menage a domicile");
        company.UpdateMissionDispatchSettings(0, true);
        company.Approve();

        var provider = CreateProvider(company.Id, service.Id, "Awa", "Konate", "+225 0543543543", "awa.konate@test.ci");

        db.Services.Add(service);
        db.Companies.Add(company);
        db.Providers.Add(provider);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Provider,
            provider.Id,
            MobileDevicePlatform.Android,
            "provider-device-token",
            "Android terrain"));
        db.CommissionRules.Add(new CommissionRule(
            "Commission mise en relation wele",
            CommissionRuleTarget.PlatformConnection,
            1500,
            0,
            "XOF"));

        await db.SaveChangesAsync();
        return new WorkflowSeed(company, provider, service, prestation);
    }

    private static ProviderProfile CreateProvider(
        Guid companyId,
        Guid serviceId,
        string firstName,
        string lastName,
        string phoneNumber,
        string? email = null)
    {
        var provider = new ProviderProfile(
            companyId,
            firstName,
            lastName,
            phoneNumber,
            email ?? $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@test.ci",
            new DateOnly(1994, 5, 10),
            "Cocody Angre",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            5,
            5.348850m,
            -4.003150m,
            5);
        provider.Approve();
        provider.SetAvailability(true, 5.348850m, -4.003150m);
        provider.AddService(serviceId, ExperienceLevel.Confirmed);
        return provider;
    }

    private static CreateClientMissionRequest CreateMissionRequest(Guid serviceId, Guid prestationId)
    {
        return new CreateClientMissionRequest(
            "Jean",
            "Client",
            ClientPhoneNumber,
            serviceId,
            prestationId,
            "Instant",
            "MobileMoney",
            null,
            90,
            "Nettoyage complet apres reception.",
            "Cocody Angre, Abidjan",
            5.348850m,
            -4.003150m,
            RequiresCompanyQuote: true,
            IsUrgent: true,
            [
                new ClientMissionPhotoRequest(
                    "salon.jpg",
                    "client-missions/pending/salon.jpg",
                    "image/jpeg",
                    2048,
                    "Salon a nettoyer")
            ]);
    }

    private const string ClientPhoneNumber = "+2250700000002";

    private sealed record WorkflowSeed(
        Company Company,
        ProviderProfile Provider,
        Service Service,
        ServicePrestation Prestation);

    private sealed record ConfirmedMissionScenario(Guid MissionId, Guid CompanyId);

    private sealed record CustomerValidatedMissionScenario(Guid MissionId, Guid CompanyId, Guid CustomerId);
}
