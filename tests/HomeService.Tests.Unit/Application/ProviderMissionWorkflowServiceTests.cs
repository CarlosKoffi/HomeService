using System.Reflection;
using HomeService.Application.ProviderPortal;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Application;

public sealed class ProviderMissionWorkflowServiceTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CompanyId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly ProviderMissionWorkflowService _service = new();

    [Fact]
    public void StartMission_WhenEverythingIsValid_StartsAssignmentAndMission()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        var request = ValidLocation();

        var result = _service.StartMission(provider, assignment, request);

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.True(result.Response!.IsVerified);
        Assert.Equal(ProviderMissionAssignmentStatus.Started, assignment.Status);
        Assert.Equal(MissionStatus.Started, mission.Status);
    }

    [Fact]
    public void VerifyArrival_WhenPaymentIsPending_IsRejectedWithoutLocationMutation()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission, confirmPayment: false);

        var result = _service.VerifyArrival(provider, assignment, ValidLocation());

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Contains("paiement", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LocationVerificationStatus.NotChecked, assignment.ArrivalVerificationStatus);
        Assert.Null(assignment.ArrivalLatitude);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
    }

    [Fact]
    public void UpdatePosition_WhenPaymentIsConfirmed_UpdatesProviderAndMarksMissionOnTheWay()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        var request = new ProviderLocationVerificationRequest(5.360000m, -4.010000m, 18);

        var result = _service.UpdatePosition(provider, assignment, request);

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.Equal(MissionStatus.OnTheWay, mission.Status);
        Assert.Equal(5.360000m, provider.CurrentLatitude);
        Assert.Equal(-4.010000m, provider.CurrentLongitude);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
    }

    [Fact]
    public void UpdatePosition_WhenPaymentIsPending_IsRejectedWithoutExposingNewPosition()
    {
        var provider = CreateApprovedProvider();
        var initialLatitude = provider.CurrentLatitude;
        var initialLongitude = provider.CurrentLongitude;
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission, confirmPayment: false);

        var result = _service.UpdatePosition(
            provider,
            assignment,
            new ProviderLocationVerificationRequest(5.360000m, -4.010000m, 18));

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Contains("paiement", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
        Assert.Equal(initialLatitude, provider.CurrentLatitude);
        Assert.Equal(initialLongitude, provider.CurrentLongitude);
    }

    [Fact]
    public void StartMission_WhenPaymentIsPending_IsRejectedWithoutLocationMutation()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission, confirmPayment: false);

        var result = _service.StartMission(provider, assignment, ValidLocation());

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Contains("paiement", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LocationVerificationStatus.NotChecked, assignment.ArrivalVerificationStatus);
        Assert.Null(assignment.ArrivalLatitude);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
    }

    [Fact]
    public void CompleteMission_WhenMissionIsStarted_CompletesAssignmentAndMission()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        _service.StartMission(provider, assignment, ValidLocation());
        provider.SetAvailability(false, provider.CurrentLatitude, provider.CurrentLongitude);

        var result = _service.CompleteMission(
            provider,
            assignment,
            new ProviderCompleteMissionRequest(75, "Intervention terminee.", "/storage/photo.jpg"));

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Completed, assignment.Status);
        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Equal("Intervention terminee.", assignment.CompletionNote);
        Assert.Equal("/storage/photo.jpg", assignment.CompletionPhotoPath);
        Assert.Equal(75, mission.ActualDurationMinutes);
        Assert.True(provider.IsAvailable);
    }

    [Fact]
    public void CompleteMission_WhenDurationIsInvalid_IsRejected()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        _service.StartMission(provider, assignment, ValidLocation());

        var result = _service.CompleteMission(
            provider,
            assignment,
            new ProviderCompleteMissionRequest(0, null, null));

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Started, assignment.Status);
        Assert.Equal(MissionStatus.Started, mission.Status);
    }

    [Fact]
    public void CompleteMission_WhenAdditionalQuoteIsOpen_KeepsMissionPaused()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        _service.StartMission(provider, assignment, ValidLocation());

        var result = _service.CompleteMission(
            provider,
            assignment,
            new ProviderCompleteMissionRequest(60, "Intervention terminee.", null),
            hasBlockingAdditionalQuote: true);

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Contains("pause", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProviderMissionAssignmentStatus.Started, assignment.Status);
        Assert.Equal(MissionStatus.Started, mission.Status);
    }

    [Fact]
    public void AcceptMission_WhenLocationIsValid_AcceptsAssignmentAndMissionWithoutReleasingContacts()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateOfferedAssignment(mission);

        var result = _service.AcceptMission(provider, assignment, new ProviderAcceptMissionRequest(5.348800m, -4.003100m, 25));

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
        Assert.NotNull(mission.ProviderAcceptedAt);
        Assert.False(mission.CanRevealContactDetails);
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public void AcceptMission_WhenLocationIsInvalid_DoesNotMutateAssignmentOrMission()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateOfferedAssignment(mission);

        var result = _service.AcceptMission(provider, assignment, new ProviderAcceptMissionRequest(95m, -4.003100m, 25));

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Offered, assignment.Status);
        Assert.Equal(MissionStatus.Assigned, mission.Status);
        Assert.Null(mission.ProviderAcceptedAt);
    }

    [Fact]
    public void RefuseMission_WhenReasonIsValid_RefusesAssignmentAndReleasesMissionProvider()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateOfferedAssignment(mission);
        var request = new ProviderRefuseMissionRequest(nameof(ProviderMissionRefusalReason.TooFar), "Le client est trop loin.");

        var result = _service.RefuseMission(provider, assignment, request);

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Refused, assignment.Status);
        Assert.Equal(ProviderMissionRefusalReason.TooFar, assignment.RefusalReason);
        Assert.Equal("Le client est trop loin.", assignment.RefusalComment);
        Assert.NotNull(assignment.RespondedAt);
        Assert.Equal(MissionStatus.SearchingProvider, mission.Status);
        Assert.Null(mission.ProviderId);
        Assert.Null(mission.ProviderAcceptedAt);
    }

    [Fact]
    public void RefuseMission_WhenReasonIsOtherWithoutComment_IsRejected()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateOfferedAssignment(mission);
        var request = new ProviderRefuseMissionRequest(nameof(ProviderMissionRefusalReason.Other), " ");

        var result = _service.RefuseMission(provider, assignment, request);

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Offered, assignment.Status);
        Assert.Null(assignment.RefusalReason);
        Assert.Null(assignment.RespondedAt);
    }

    [Fact]
    public void RefuseMission_WhenAssignmentIsAlreadyAccepted_IsRejected()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        var request = new ProviderRefuseMissionRequest(nameof(ProviderMissionRefusalReason.Unavailable), "Plus disponible.");

        var result = _service.RefuseMission(provider, assignment, request);

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Null(assignment.RefusalReason);
    }

    [Fact]
    public void StartMission_WhenCalledTwice_ReturnsOkWithoutChangingProof()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        var request = ValidLocation();

        var first = _service.StartMission(provider, assignment, request);
        var firstVerifiedAt = assignment.ArrivalVerifiedAt;
        var second = _service.StartMission(provider, assignment, new ProviderLocationVerificationRequest(5.390000m, -4.050000m, 20));

        Assert.Equal(ProviderMissionOperationStatus.Ok, first.Status);
        Assert.Equal(ProviderMissionOperationStatus.Ok, second.Status);
        Assert.Equal(LocationVerificationStatus.Verified, assignment.ArrivalVerificationStatus);
        Assert.Equal(firstVerifiedAt, assignment.ArrivalVerifiedAt);
    }

    [Fact]
    public void VerifyArrival_AfterMissionStarted_DoesNotOverwriteVerifiedArrival()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        _service.StartMission(provider, assignment, ValidLocation());
        var verifiedDistance = assignment.ArrivalDistanceMeters;

        var result = _service.VerifyArrival(provider, assignment, new ProviderLocationVerificationRequest(5.390000m, -4.050000m, 20));

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.Equal(LocationVerificationStatus.Verified, assignment.ArrivalVerificationStatus);
        Assert.Equal(verifiedDistance, assignment.ArrivalDistanceMeters);
    }

    [Fact]
    public void StartMission_WhenProviderIsSuspended_IsForbidden()
    {
        var provider = CreateApprovedProvider();
        provider.SuspendByCompany();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);

        var result = _service.StartMission(provider, assignment, ValidLocation());

        Assert.Equal(ProviderMissionOperationStatus.Forbidden, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(MissionStatus.Accepted, mission.Status);
    }

    [Fact]
    public void StartMission_WhenMissionIsCancelled_IsRejected()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        SetProperty(mission, nameof(Mission.Status), MissionStatus.Cancelled);

        var result = _service.StartMission(provider, assignment, ValidLocation());

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
    }

    [Fact]
    public void StartMission_WhenLocationIsOutsideTolerance_ReturnsBadRequestWithProof()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        var farAway = new ProviderLocationVerificationRequest(5.390000m, -4.050000m, 20);

        var result = _service.StartMission(provider, assignment, farAway);

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.NotNull(result.Response);
        Assert.False(result.Response.IsVerified);
        Assert.Equal(LocationVerificationStatus.OutsideTolerance.ToString(), result.Response.Status);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
    }

    [Fact]
    public void StartMission_WhenLocationPayloadIsInvalid_DoesNotMutateAssignment()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);
        var invalid = new ProviderLocationVerificationRequest(95m, -4.003150m, 20);

        var result = _service.StartMission(provider, assignment, invalid);

        Assert.Equal(ProviderMissionOperationStatus.BadRequest, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(LocationVerificationStatus.NotChecked, assignment.ArrivalVerificationStatus);
        Assert.Null(assignment.ArrivalLatitude);
    }

    [Fact]
    public void VerifyArrival_WhenBadGpsThenGoodGps_CanRecoverToVerified()
    {
        var provider = CreateApprovedProvider();
        var mission = CreateAssignedMission();
        var assignment = CreateAcceptedAssignment(mission);

        assignment.VerifyArrival(5.348850m, -4.003150m, 200, mission.ServiceLatitude, mission.ServiceLongitude, mission.ArrivalToleranceMeters);
        var result = _service.VerifyArrival(provider, assignment, ValidLocation());

        Assert.Equal(ProviderMissionOperationStatus.Ok, result.Status);
        Assert.True(assignment.HasVerifiedArrival);
        Assert.Equal(LocationVerificationStatus.Verified, assignment.ArrivalVerificationStatus);
    }

    private static ProviderLocationVerificationRequest ValidLocation()
    {
        return new ProviderLocationVerificationRequest(5.348850m, -4.003150m, 25);
    }

    private static ProviderProfile CreateApprovedProvider()
    {
        var company = new Company("Kaza Services", "+2250700000000", "ops@kaza.ci");
        company.Approve();

        var provider = new ProviderProfile(
            CompanyId,
            "Awa",
            "Kone",
            "+2250701020304",
            "awa.kone@kaza.ci",
            new DateOnly(1995, 1, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            5.348850m,
            -4.003150m,
            5);

        provider.Approve();
        provider.SetAvailability(true, 5.348850m, -4.003150m);
        SetProperty(provider, nameof(ProviderProfile.Company), company);
        return provider;
    }

    private static Mission CreateAssignedMission()
    {
        var mission = new Mission(CustomerId, ServiceId, MissionMode.Instant, PaymentMethod.MobileMoney, null, 90);
        mission.SetServiceLocation("Cocody Angre", 5.348850m, -4.003150m, 250);
        mission.Assign(ProviderId, CompanyId, 10000);
        return mission;
    }

    private static ProviderMissionAssignment CreateAcceptedAssignment(Mission mission, bool confirmPayment = true)
    {
        var assignment = CreateOfferedAssignment(mission);
        assignment.Accept(5.348800m, -4.003100m, 25);
        mission.MarkProviderAccepted(ProviderId, CompanyId);
        if (confirmPayment)
        {
            mission.ConfirmByCustomer(0, 0);
        }
        return assignment;
    }

    private static ProviderMissionAssignment CreateOfferedAssignment(Mission mission)
    {
        var assignment = new ProviderMissionAssignment(mission.Id, ProviderId, CompanyId, DateTimeOffset.UtcNow.AddMinutes(3));
        SetProperty(assignment, nameof(ProviderMissionAssignment.Mission), mission);
        return assignment;
    }

    private static void SetProperty<T>(object instance, string propertyName, T value)
    {
        instance.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }
}
