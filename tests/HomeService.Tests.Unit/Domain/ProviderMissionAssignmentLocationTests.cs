using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class ProviderMissionAssignmentLocationTests
{
    private static readonly Guid MissionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProviderId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void VerifyArrival_WhenProviderIsInsideTolerance_MarksArrivalAsVerified()
    {
        var assignment = CreateAcceptedAssignment();

        assignment.VerifyArrival(
            providerLatitude: 5.348800m,
            providerLongitude: -4.003100m,
            accuracyMeters: 20,
            missionLatitude: 5.348850m,
            missionLongitude: -4.003150m,
            toleranceMeters: 250);

        Assert.True(assignment.HasVerifiedArrival);
        Assert.Equal(LocationVerificationStatus.Verified, assignment.ArrivalVerificationStatus);
        Assert.NotNull(assignment.ArrivalDistanceMeters);
        Assert.True(assignment.ArrivalDistanceMeters <= 250);
        Assert.Equal(250, assignment.ArrivalToleranceMeters);
        Assert.NotNull(assignment.ArrivalVerifiedAt);
    }

    [Fact]
    public void VerifyArrival_WhenProviderIsOutsideTolerance_KeepsMissionBlocked()
    {
        var assignment = CreateAcceptedAssignment();

        assignment.VerifyArrival(
            providerLatitude: 5.370000m,
            providerLongitude: -4.030000m,
            accuracyMeters: 20,
            missionLatitude: 5.348850m,
            missionLongitude: -4.003150m,
            toleranceMeters: 250);

        Assert.False(assignment.HasVerifiedArrival);
        Assert.Equal(LocationVerificationStatus.OutsideTolerance, assignment.ArrivalVerificationStatus);
        Assert.NotNull(assignment.ArrivalDistanceMeters);
        Assert.True(assignment.ArrivalDistanceMeters > 250);
    }

    [Fact]
    public void VerifyArrival_WhenProviderLocationIsMissing_ReturnsMissingProviderLocation()
    {
        var assignment = CreateAcceptedAssignment();

        assignment.VerifyArrival(
            providerLatitude: null,
            providerLongitude: -4.003100m,
            accuracyMeters: 20,
            missionLatitude: 5.348850m,
            missionLongitude: -4.003150m,
            toleranceMeters: 250);

        Assert.False(assignment.HasVerifiedArrival);
        Assert.Equal(LocationVerificationStatus.MissingProviderLocation, assignment.ArrivalVerificationStatus);
        Assert.Null(assignment.ArrivalDistanceMeters);
    }

    [Fact]
    public void VerifyArrival_WhenMissionLocationIsMissing_ReturnsMissingMissionLocation()
    {
        var assignment = CreateAcceptedAssignment();

        assignment.VerifyArrival(
            providerLatitude: 5.348800m,
            providerLongitude: -4.003100m,
            accuracyMeters: 20,
            missionLatitude: null,
            missionLongitude: -4.003150m,
            toleranceMeters: 250);

        Assert.False(assignment.HasVerifiedArrival);
        Assert.Equal(LocationVerificationStatus.MissingMissionLocation, assignment.ArrivalVerificationStatus);
        Assert.Null(assignment.ArrivalDistanceMeters);
    }

    [Fact]
    public void VerifyArrival_WhenGpsAccuracyIsTooWeak_DoesNotMarkAsVerified()
    {
        var assignment = CreateAcceptedAssignment();

        assignment.VerifyArrival(
            providerLatitude: 5.348800m,
            providerLongitude: -4.003100m,
            accuracyMeters: 200,
            missionLatitude: 5.348850m,
            missionLongitude: -4.003150m,
            toleranceMeters: 250);

        Assert.False(assignment.HasVerifiedArrival);
        Assert.Equal(LocationVerificationStatus.LowAccuracy, assignment.ArrivalVerificationStatus);
        Assert.NotNull(assignment.ArrivalDistanceMeters);
    }

    [Theory]
    [InlineData(10, 50)]
    [InlineData(250, 250)]
    [InlineData(5000, 2000)]
    public void VerifyArrival_ClampsToleranceToOperationalLimits(int requestedToleranceMeters, int expectedToleranceMeters)
    {
        var assignment = CreateAcceptedAssignment();

        assignment.VerifyArrival(
            providerLatitude: 5.348800m,
            providerLongitude: -4.003100m,
            accuracyMeters: 20,
            missionLatitude: 5.348850m,
            missionLongitude: -4.003150m,
            toleranceMeters: requestedToleranceMeters);

        Assert.Equal(expectedToleranceMeters, assignment.ArrivalToleranceMeters);
    }

    private static ProviderMissionAssignment CreateAcceptedAssignment()
    {
        var assignment = new ProviderMissionAssignment(MissionId, ProviderId, CompanyId, DateTimeOffset.UtcNow.AddMinutes(3));
        assignment.Accept(5.348700m, -4.003000m, 25);
        return assignment;
    }
}
