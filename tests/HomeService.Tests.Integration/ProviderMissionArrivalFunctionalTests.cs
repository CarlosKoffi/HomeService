using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Integration;

public sealed class ProviderMissionArrivalFunctionalTests
{
    [Fact]
    public void AcceptedLiveMission_VerifiedArrival_CanMoveToStartedState()
    {
        var mission = CreateMissionWithClientLocation();
        var assignment = CreateAssignment(mission.Id);

        assignment.CaptureOfferLocation(5.348600m, -4.003000m, 35);
        assignment.Accept(5.348700m, -4.003050m, 25);
        assignment.VerifyArrival(
            providerLatitude: 5.348830m,
            providerLongitude: -4.003130m,
            accuracyMeters: 18,
            missionLatitude: mission.ServiceLatitude,
            missionLongitude: mission.ServiceLongitude,
            toleranceMeters: mission.ArrivalToleranceMeters);

        Assert.True(assignment.HasVerifiedArrival);

        assignment.Start();

        Assert.Equal(ProviderMissionAssignmentStatus.Started, assignment.Status);
        Assert.NotNull(assignment.StartedAt);
        Assert.Equal(LocationVerificationStatus.Verified, assignment.ArrivalVerificationStatus);
        Assert.NotNull(assignment.OfferedLatitude);
        Assert.NotNull(assignment.AcceptedLatitude);
        Assert.NotNull(assignment.ArrivalLatitude);
    }

    [Fact]
    public void AcceptedLiveMission_OutsideClientZone_StaysBlockedBeforeStart()
    {
        var mission = CreateMissionWithClientLocation();
        var assignment = CreateAssignment(mission.Id);

        assignment.Accept(5.348700m, -4.003050m, 25);
        assignment.VerifyArrival(
            providerLatitude: 5.390000m,
            providerLongitude: -4.050000m,
            accuracyMeters: 18,
            missionLatitude: mission.ServiceLatitude,
            missionLongitude: mission.ServiceLongitude,
            toleranceMeters: mission.ArrivalToleranceMeters);

        Assert.False(assignment.HasVerifiedArrival);
        Assert.Equal(ProviderMissionAssignmentStatus.Accepted, assignment.Status);
        Assert.Equal(LocationVerificationStatus.OutsideTolerance, assignment.ArrivalVerificationStatus);
        Assert.True(assignment.ArrivalDistanceMeters > mission.ArrivalToleranceMeters);
    }

    [Fact]
    public void MissionLocation_AllowsBusinessSpecificArrivalTolerance()
    {
        var mission = CreateMissionWithClientLocation();

        mission.SetServiceLocation(
            "Cocody Angre, pharmacie du rond-point",
            5.348850m,
            -4.003150m,
            arrivalToleranceMeters: 400);

        var assignment = CreateAssignment(mission.Id);
        assignment.Accept(5.348700m, -4.003050m, 25);
        assignment.VerifyArrival(
            providerLatitude: 5.351700m,
            providerLongitude: -4.005300m,
            accuracyMeters: 30,
            missionLatitude: mission.ServiceLatitude,
            missionLongitude: mission.ServiceLongitude,
            toleranceMeters: mission.ArrivalToleranceMeters);

        Assert.Equal(400, mission.ArrivalToleranceMeters);
        Assert.Equal(400, assignment.ArrivalToleranceMeters);
        Assert.True(assignment.HasVerifiedArrival);
    }

    private static Mission CreateMissionWithClientLocation()
    {
        var mission = new Mission(
            customerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            serviceId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            mode: MissionMode.Instant,
            paymentMethod: PaymentMethod.MobileMoney,
            scheduledFor: null,
            estimatedDurationMinutes: 90);

        mission.SetServiceLocation(
            "Cocody Angre, pharmacie du rond-point",
            5.348850m,
            -4.003150m,
            arrivalToleranceMeters: 250);

        return mission;
    }

    private static ProviderMissionAssignment CreateAssignment(Guid missionId)
    {
        return new ProviderMissionAssignment(
            missionId,
            providerId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            companyId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(3));
    }
}
