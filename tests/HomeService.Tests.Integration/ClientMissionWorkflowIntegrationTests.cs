using HomeService.Application.Admin;
using HomeService.Application.Auditing;
using HomeService.Application.Clients;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Application.ProviderPortal;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Missions;
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
        await SelectCustomerPaymentMethodAsync(db, creation.Response.MissionId);

        var createdMission = await db.Missions
            .AsNoTracking()
            .SingleAsync(mission => mission.Id == creation.Response.MissionId);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            createdMission.CustomerId,
            MobileDevicePlatform.Android,
            "customer-device-token",
            "Android client"));
        await db.SaveChangesAsync();

        var offerService = new CompanyMissionOfferService(db);
        var openOffers = await offerService.ListOpenOffersAsync(seed.Company.Id, CancellationToken.None);
        Assert.True(openOffers.IsSuccess);
        Assert.Single(openOffers.Offers);

        var acceptedOffer = await offerService.AcceptAsync(seed.Company.Id, openOffers.Offers[0].OfferId!.Value, CancellationToken.None);
        Assert.True(acceptedOffer.IsSuccess);
        Assert.NotNull(acceptedOffer.Response);

        var portalMissions = await new CompanyPortalQueryService(db).ListMissionsAsync(
            seed.Company.Id,
            "live",
            CancellationToken.None);
        Assert.True(portalMissions.IsSuccess);
        Assert.Contains(portalMissions.Missions, mission => mission.Id == acceptedOffer.Response.MissionId);

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
        var providerNotifications = CreateProviderNotificationService(db);
        var assignment = await LoadAssignmentAsync(db, assignmentResult.Response.AssignmentId);
        var provider = await LoadProviderAsync(db, seed.Provider.Id);
        var acceptResult = workflow.AcceptMission(
            provider,
            assignment,
            new ProviderAcceptMissionRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, acceptResult.Status);
        await providerNotifications.NotifyAcceptedAsync(assignment.Mission!, provider, assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        var confirmation = await CreateClientConfirmationService(db).ConfirmAsync(
            acceptedOffer.Response.MissionId,
            new ConfirmClientMissionRequest(ClientPhoneNumber, "MM-MOCK-001"),
            CancellationToken.None);
        Assert.True(confirmation.IsSuccess);
        Assert.NotNull(confirmation.Response);
        Assert.Equal(12_000, confirmation.Response.ServiceAndPartsAmount);
        Assert.Equal(900, confirmation.Response.CustomerServiceFeeAmount);
        Assert.Equal(12_900, confirmation.Response.TotalAmount);
        Assert.Equal(1800, assignment.Mission!.PlatformCommissionAmount);
        Assert.Equal(10200, assignment.Mission.CompanyPayoutAmount);
        Assert.True(confirmation.Response.ContactDetailsReleased);

        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == seed.Company.Id
            && notification.Type == "MissionQuoteAcceptedByCustomer");

        assignment = await LoadAssignmentAsync(db, assignmentResult.Response.AssignmentId);
        provider = await LoadProviderAsync(db, seed.Provider.Id);
        var arrivalResult = workflow.VerifyArrival(
            provider,
            assignment,
            new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, arrivalResult.Status);
        await providerNotifications.NotifyArrivedAsync(assignment.Mission!, provider, assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        assignment = await LoadAssignmentAsync(db, assignmentResult.Response.AssignmentId);
        provider = await LoadProviderAsync(db, seed.Provider.Id);
        var startResult = workflow.StartMission(
            provider,
            assignment,
            new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, startResult.Status);
        await new MissionPaymentMilestoneService(db).EnsureMissionStartedMilestoneAsync(assignment.Mission!, CancellationToken.None);
        await providerNotifications.NotifyStartedAsync(assignment.Mission!, provider, assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        var completeResult = workflow.CompleteMission(
            provider,
            assignment,
            new ProviderCompleteMissionRequest(90, "Intervention terminee proprement.", "missions/MIS/photo-fin.jpg"));
        Assert.Equal(ProviderMissionOperationStatus.Ok, completeResult.Status);
        await new MissionPaymentMilestoneService(db).EnsureMissionCompletedMilestoneAsync(assignment.Mission!, CancellationToken.None);
        await providerNotifications.NotifyCompletedAsync(assignment.Mission!, provider, assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        var validation = await CreateCompletionValidationService(db).ValidateAsync(
            acceptedOffer.Response.MissionId,
            new ValidateClientMissionCompletionRequest(ClientPhoneNumber, 5, 5, 5, 4, 5, "Tres bon service.", "PAYOUT-MOCK-001"),
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

        var completedPortalMission = Assert.Single((await new CompanyPortalQueryService(db).ListMissionsAsync(
            seed.Company.Id,
            "past",
            CancellationToken.None)).Missions);
        Assert.Empty(completedPortalMission.CustomerPhoneNumber);
        Assert.Null(completedPortalMission.LocationLabel);
        Assert.Null(completedPortalMission.ServiceLatitude);
        Assert.Null(completedPortalMission.ServiceLongitude);

        var completedPortalDetail = await new CompanyPortalQueryService(db).GetMissionDetailAsync(
            seed.Company.Id,
            mission.Id,
            CancellationToken.None);
        Assert.True(completedPortalDetail.IsSuccess);
        Assert.Empty(completedPortalDetail.Response!.Mission.CustomerPhoneNumber);
        Assert.Null(completedPortalDetail.Response.Mission.LocationLabel);
        Assert.Null(completedPortalDetail.Response.Mission.ServiceLatitude);
        Assert.Null(completedPortalDetail.Response.Mission.ServiceLongitude);
        Assert.Null(completedPortalDetail.Response.ProviderDistanceKilometers);

        var review = await db.MissionReviews.SingleAsync(review => review.MissionId == mission.Id);
        Assert.Equal(5, review.QualityRating);
        Assert.Equal(5, review.PunctualityRating);
        Assert.Equal(4, review.PolitenessRating);
        Assert.Equal(5, review.CleanlinessRating);
        Assert.Equal(5, review.OverallRating);
        Assert.Equal("Tres bon service.", review.Comment);
        Assert.Equal(seed.Company.Id, review.CompanyId);
        Assert.Equal(seed.Provider.Id, review.ProviderId);

        var completionMilestone = await db.MissionPaymentMilestones.SingleAsync(milestone =>
            milestone.MissionId == mission.Id
            && milestone.Trigger == MissionPaymentMilestoneTrigger.MissionCompleted);
        Assert.Equal(MissionPaymentMilestoneStatus.Paid, completionMilestone.Status);
        Assert.Equal("PAYOUT-MOCK-001", completionMilestone.ExternalPaymentReference);

        Assert.Contains(await db.CompanyPortalActivities.ToListAsync(), activity =>
            activity.CompanyId == seed.Company.Id
            && activity.Title == "Mission validee par le client"
            && activity.Description.Contains("5/5", StringComparison.OrdinalIgnoreCase));

        var mobilePushMessages = await db.NotificationOutboxMessages
            .Where(message => message.Channel == NotificationChannel.MobilePush)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync();
        Assert.Equal(7, mobilePushMessages.Count);
        Assert.All(mobilePushMessages, message => Assert.Equal(NotificationStatus.Pending, message.Status));
        Assert.Contains(mobilePushMessages, message => message.Subject.Contains("Nouvelle mission", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mobilePushMessages, message => message.Subject.Contains("prestataire", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mobilePushMessages, message => message.Subject.Contains("Mission confirmee", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mobilePushMessages, message => message.MetadataJson!.Contains("mission_technician_arrived"));
        Assert.Contains(mobilePushMessages, message => message.MetadataJson!.Contains("mission_started"));
        Assert.Contains(mobilePushMessages, message => message.Subject.Contains("Mission terminee", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mobilePushMessages, message => message.Subject.Contains("Mission validee", StringComparison.OrdinalIgnoreCase));
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
            && activity.Title == "Mission annulée par le client"
            && activity.Description.Contains("Client indisponible", StringComparison.Ordinal));

        var portalMission = Assert.Single((await new CompanyPortalQueryService(db).ListMissionsAsync(
            scenario.CompanyId,
            "past",
            CancellationToken.None)).Missions);
        Assert.Equal("Customer", portalMission.CancellationActor);
        Assert.Equal("Other", portalMission.CancellationReason);
        Assert.Equal("Client indisponible.", portalMission.CancellationComment);
        Assert.Empty(portalMission.CustomerPhoneNumber);
        Assert.Null(portalMission.LocationLabel);
        Assert.Null(portalMission.ServiceLatitude);
        Assert.Null(portalMission.ServiceLongitude);
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
        var refundPush = Assert.Single(customerNotifications, message =>
            message.Channel == NotificationChannel.MobilePush
            && message.Subject == "Remboursement valide");
        Assert.Contains("XOF", refundPush.Body);
        Assert.Contains(mission.MissionNumber, refundPush.Body);
        Assert.Contains($$""""missionNumber":"{{mission.MissionNumber}}"""", refundPush.MetadataJson);
        Assert.Contains("\"refundAmount\":3000", refundPush.MetadataJson);

        var refundWhatsApp = Assert.Single(customerNotifications, message =>
            message.Channel == NotificationChannel.WhatsApp
            && message.Subject == "Remboursement valide");
        Assert.Equal(ClientPhoneNumber, refundWhatsApp.Recipient);
        Assert.Contains("XOF", refundWhatsApp.Body);
        Assert.Contains(mission.MissionNumber, refundWhatsApp.MetadataJson);

        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == completedMission.CompanyId
            && notification.Type == "MissionDisputeResolved");
    }

    [Fact]
    public async Task AdditionalQuote_DuringStartedMission_CanBeRequestedSubmittedAndPaidWithMockedNotifications()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateConfirmedMissionScenarioAsync(db);
        var assignment = await db.ProviderMissionAssignments
            .Include(item => item.Mission)
            .SingleAsync(item => item.MissionId == scenario.MissionId);
        var provider = await LoadProviderAsync(db, assignment.ProviderId);
        db.MobileDeviceTokens.Add(new MobileDeviceToken(
            MobileDeviceOwnerType.Customer,
            assignment.Mission!.CustomerId,
            MobileDevicePlatform.Android,
            "customer-additional-quote-device-token",
            "Telephone client complement"));
        await db.SaveChangesAsync();

        var startResult = new ProviderMissionWorkflowService().StartMission(
            provider,
            assignment,
            new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 18));
        Assert.Equal(ProviderMissionOperationStatus.Ok, startResult.Status);
        await db.SaveChangesAsync();

        var quoteService = CreateAdditionalQuoteService(db);
        var requestResult = await quoteService.RequestFromProviderAsync(
            provider.Id,
            scenario.MissionId,
            new RequestMissionAdditionalQuoteRequest(
                "Il faut ajouter un produit specifique pour terminer proprement.",
                "missions/additional/produit.jpg"),
            CancellationToken.None);
        Assert.True(requestResult.IsSuccess);
        Assert.NotNull(requestResult.Response);
        Assert.Equal("Requested", requestResult.Response.Status);

        var submitResult = await quoteService.SubmitByCompanyAsync(
            scenario.CompanyId,
            requestResult.Response.Id,
            new SubmitMissionAdditionalQuoteRequest(
                4500,
                "XOF",
                "Produit complementaire et temps d'application."),
            CancellationToken.None);
        Assert.True(submitResult.IsSuccess);
        Assert.Equal("Submitted", submitResult.Response!.Status);

        var clientStatusBeforePayment = await new ClientMissionStatusService(db).GetAsync(
            scenario.MissionId,
            ClientPhoneNumber,
            CancellationToken.None);
        Assert.True(clientStatusBeforePayment.IsSuccess);
        var payableQuote = Assert.Single(clientStatusBeforePayment.Response!.AdditionalQuotes);
        Assert.True(payableQuote.CanPay);
        Assert.Equal(4500, payableQuote.Amount);

        var payResult = await quoteService.PayByCustomerAsync(
            requestResult.Response.Id,
            new PayMissionAdditionalQuoteRequest(ClientPhoneNumber, "MM-MOCK-ADD-001"),
            CancellationToken.None);
        Assert.True(payResult.IsSuccess);
        Assert.Equal("Paid", payResult.Response!.Status);

        var clientStatusAfterPayment = await new ClientMissionStatusService(db).GetAsync(
            scenario.MissionId,
            ClientPhoneNumber,
            CancellationToken.None);
        Assert.False(Assert.Single(clientStatusAfterPayment.Response!.AdditionalQuotes).CanPay);

        var milestone = await db.MissionPaymentMilestones.SingleAsync(item =>
            item.MissionId == scenario.MissionId
            && item.Trigger == MissionPaymentMilestoneTrigger.AdditionalQuote);
        Assert.Equal(MissionPaymentMilestoneStatus.Paid, milestone.Status);
        Assert.Equal(4500, milestone.Amount);

        Assert.Contains(await db.MissionFinancialBreakdowns.ToListAsync(), line =>
            line.MissionId == scenario.MissionId
            && line.LineType == MissionFinancialLineType.AdditionalQuote
            && line.Amount == 4500);
        Assert.Contains(await db.CompanyPortalNotifications.ToListAsync(), notification =>
            notification.CompanyId == scenario.CompanyId
            && notification.Type == "MissionAdditionalQuotePaid");
        Assert.Contains(await db.NotificationOutboxMessages.ToListAsync(), message =>
            message.Channel == NotificationChannel.MobilePush
            && message.RelatedEntityType == nameof(MissionAdditionalQuote));
    }

    [Fact]
    public async Task ProviderRefusal_RemovesProviderFromSameMissionAndAllowsAnotherProviderAssignment()
    {
        await using var db = CreateDbContext();
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);
        var secondProvider = CreateProvider(seed.Company.Id, seed.Service.Id, seed.Prestation.Id, "Mamadou", "Diallo", "+225 0555000011");
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
        var acceptedOffer = await offerService.AcceptAsync(seed.Company.Id, offers.Offers[0].OfferId!.Value, CancellationToken.None);
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
        var refusedProvider = Assert.Single(assignableAfterRefusal.Providers, provider => provider.Id == seed.Provider.Id);
        Assert.False(refusedProvider.CanAssign);
        Assert.Contains("deja refuse", refusedProvider.BlockingReason);
        Assert.Contains(assignableAfterRefusal.Providers, provider => provider.Id == secondProvider.Id && provider.CanAssign);

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

    [Fact]
    public async Task ProviderAssignmentTimeout_BlocksLateAcceptanceAndReleasesMissionForAnotherCompanyRound()
    {
        await using var db = CreateDbContext();
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);
        var secondProvider = CreateProvider(seed.Company.Id, seed.Service.Id, seed.Prestation.Id, "Mariam", "Coulibaly", "+225 0555000022");
        db.Providers.Add(secondProvider);
        await db.SaveChangesAsync();

        var creation = await CreateClientRequestService(db).CreateAsync(
            CreateMissionRequest(seed.Service.Id, seed.Prestation.Id),
            CancellationToken.None);
        Assert.True(creation.IsSuccess);

        var offerService = new CompanyMissionOfferService(db);
        var offers = await offerService.ListOpenOffersAsync(seed.Company.Id, CancellationToken.None);
        var acceptedOffer = await offerService.AcceptAsync(seed.Company.Id, offers.Offers[0].OfferId!.Value, CancellationToken.None);
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

        var expiration = await new ProviderAssignmentExpirationService(db).ExpireDueAssignmentsAsync(
            firstAssignment.Response!.ExpiresAt.AddSeconds(1),
            batchSize: 10,
            CancellationToken.None);
        Assert.Equal(1, expiration.ExpiredAssignmentCount);

        var expiredAssignment = await LoadAssignmentAsync(db, firstAssignment.Response.AssignmentId);
        var lateProvider = await LoadProviderAsync(db, seed.Provider.Id);
        var lateAccept = new ProviderMissionWorkflowService().AcceptMission(
            lateProvider,
            expiredAssignment,
            new ProviderAcceptMissionRequest(5.348850m, -4.003150m, 18));

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, lateAccept.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Expired, expiredAssignment.Status);

        var missionAfterTimeout = await db.Missions.SingleAsync(mission => mission.Id == acceptedOffer.Response.MissionId);
        Assert.Equal(MissionStatus.Offered, missionAfterTimeout.Status);
        Assert.Null(missionAfterTimeout.ProviderId);
        Assert.Null(missionAfterTimeout.CompanyId);
        var timedOutOffer = await db.MissionDispatchOffers.SingleAsync(offer => offer.Id == offers.Offers[0].OfferId);
        Assert.Equal(MissionDispatchOfferStatus.AssignmentTimedOut, timedOutOffer.Status);
    }

    [Fact]
    public async Task ClientMissionCreation_DispatchesToThreeEligibleCompaniesByReceptionPriority()
    {
        await using var db = CreateDbContext();
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);
        seed.Company.UpdateMissionDispatchSettings(30, true);

        var priorityCompany = CreateCompanyWithProvider(
            db,
            seed.Service.Id,
            seed.Prestation.Id,
            "Abidjan Clean Pro",
            "+225 0700000010",
            "dispatch-priority@test.ci",
            missionDispatchPriority: 1,
            "Fatou",
            "Bamba");
        var secondCompany = CreateCompanyWithProvider(
            db,
            seed.Service.Id,
            seed.Prestation.Id,
            "Lagune Services",
            "+225 0700000011",
            "dispatch-second@test.ci",
            missionDispatchPriority: 5,
            "Moussa",
            "Kouame");
        var overflowCompany = CreateCompanyWithProvider(
            db,
            seed.Service.Id,
            seed.Prestation.Id,
            "Plateau Services Plus",
            "+225 0700000012",
            "dispatch-overflow@test.ci",
            missionDispatchPriority: 20,
            "Aminata",
            "Traore");

        await db.SaveChangesAsync();

        var creation = await CreateClientRequestService(db).CreateAsync(
            CreateMissionRequest(seed.Service.Id, seed.Prestation.Id),
            CancellationToken.None);

        Assert.True(creation.IsSuccess);

        var offers = await db.MissionDispatchOffers
            .AsNoTracking()
            .Where(offer => offer.MissionId == creation.Response!.MissionId)
            .OrderBy(offer => offer.Rank)
            .ToListAsync();

        Assert.Equal(3, offers.Count);
        Assert.Equal(priorityCompany.Id, offers[0].CompanyId);
        Assert.Equal(secondCompany.Id, offers[1].CompanyId);
        Assert.Equal(overflowCompany.Id, offers[2].CompanyId);
        Assert.DoesNotContain(offers, offer => offer.CompanyId == seed.Company.Id);
        Assert.All(offers, offer => Assert.Equal(MissionDispatchOfferStatus.Sent, offer.Status));
        Assert.Equal([1, 2, 3], offers.Select(offer => offer.Rank).ToArray());
    }

    private static async Task<ConfirmedMissionScenario> CreateConfirmedMissionScenarioAsync(HomeServiceDbContext db)
    {
        var seed = await SeedApprovedCompanyProviderAndServiceAsync(db);
        var creation = await CreateClientRequestService(db).CreateAsync(
            CreateMissionRequest(seed.Service.Id, seed.Prestation.Id),
            CancellationToken.None);
        Assert.True(creation.IsSuccess);
        await SelectCustomerPaymentMethodAsync(db, creation.Response!.MissionId);

        var offerService = new CompanyMissionOfferService(db);
        var offers = await offerService.ListOpenOffersAsync(seed.Company.Id, CancellationToken.None);
        var offer = await offerService.AcceptAsync(seed.Company.Id, offers.Offers[0].OfferId!.Value, CancellationToken.None);
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
            new ValidateClientMissionCompletionRequest(ClientPhoneNumber, 5, 4, 5, 5, 4, "Service valide.", "PAYOUT-MOCK-DISPUTE"),
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

    private static MissionAdditionalQuoteWorkflowService CreateAdditionalQuoteService(HomeServiceDbContext db)
    {
        return new MissionAdditionalQuoteWorkflowService(
            db,
            new CompanyPortalNotificationWriter(db),
            new MobilePushNotificationQueueService(db));
    }

    private static ProviderMissionNotificationService CreateProviderNotificationService(HomeServiceDbContext db)
    {
        return new ProviderMissionNotificationService(
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

    private static async Task SelectCustomerPaymentMethodAsync(HomeServiceDbContext db, Guid missionId)
    {
        var mission = await db.Missions.SingleAsync(item => item.Id == missionId);
        var paymentMethod = new CustomerPaymentMethod(
            mission.CustomerId,
            PaymentMethod.MobileMoney,
            "Mobile Money test",
            "**** 0002",
            true);
        db.CustomerPaymentMethods.Add(paymentMethod);
        await db.SaveChangesAsync();

        var selection = await new ClientMissionPaymentMethodService(db).SelectAsync(
            mission.CustomerId,
            mission.Id,
            paymentMethod.Id,
            CancellationToken.None);
        Assert.True(selection.IsSuccess, selection.Message);
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

        var provider = CreateProvider(company.Id, service.Id, prestation.Id, "Awa", "Konate", "+225 0543543543", "awa.konate@test.ci");

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

    private static Company CreateCompanyWithProvider(
        HomeServiceDbContext db,
        Guid serviceId,
        Guid servicePrestationId,
        string name,
        string phone,
        string email,
        int missionDispatchPriority,
        string providerFirstName,
        string providerLastName)
    {
        var company = new Company(name, phone, email);
        company.UpdateCompanyInformation(name, "SARL", $"RCCM-{Guid.NewGuid():N}"[..16], $"DFE-{Guid.NewGuid():N}"[..12], "Abidjan", "Cocody Angre");
        company.UpdateOperations("Cocody, Angre, Abidjan", "Menage a domicile");
        company.UpdateMissionDispatchSettings(missionDispatchPriority, true);
        company.Approve();

        var provider = CreateProvider(
            company.Id,
            serviceId,
            servicePrestationId,
            providerFirstName,
            providerLastName,
            phone.Replace("0700", "0500", StringComparison.Ordinal));

        db.Companies.Add(company);
        db.Providers.Add(provider);
        return company;
    }

    private static ProviderProfile CreateProvider(
        Guid companyId,
        Guid serviceId,
        Guid servicePrestationId,
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
        var providerService = provider.Services.Single(service => service.ServiceId == serviceId);
        providerService.SyncPrestations([servicePrestationId]);
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
