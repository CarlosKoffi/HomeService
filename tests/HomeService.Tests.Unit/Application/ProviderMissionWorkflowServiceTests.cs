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
            new DateOnly(1995, 1, 12),
            "Cocody",
            ProviderGender.Female,
            ProviderEmploymentType.CompanyEmployee,
            4,
            5.348850m,
            -4.003150m,
            5);

        provider.Approve();
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

    private static ProviderMissionAssignment CreateAcceptedAssignment(Mission mission)
    {
        var assignment = CreateOfferedAssignment(mission);
        assignment.Accept(5.348800m, -4.003100m, 25);
        mission.MarkProviderAccepted(ProviderId, CompanyId);
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
