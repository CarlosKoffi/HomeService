using System.Reflection;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class MissionTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CompanyId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData(1, 500)]
    [InlineData(29, 500)]
    [InlineData(30, 500)]
    [InlineData(31, 1000)]
    [InlineData(90, 1500)]
    public void Assign_CalculatesEstimatedTotalByBillableHalfHours(int durationMinutes, int expectedAmount)
    {
        var mission = CreateMission(durationMinutes);

        mission.Assign(ProviderId, CompanyId, 1000);

        Assert.Equal(expectedAmount, mission.EstimatedTotalAmount);
        Assert.Equal(MissionStatus.Assigned, mission.Status);
    }

    [Fact]
    public void Start_WhenWrongProvider_Throws()
    {
        var mission = CreateAssignedMission();

        Assert.Throws<InvalidOperationException>(() => mission.Start(Guid.NewGuid(), CompanyId));
        Assert.Equal(MissionStatus.Assigned, mission.Status);
    }

    [Fact]
    public void Start_WhenWrongCompany_Throws()
    {
        var mission = CreateAssignedMission();

        Assert.Throws<InvalidOperationException>(() => mission.Start(ProviderId, Guid.NewGuid()));
        Assert.Equal(MissionStatus.Assigned, mission.Status);
    }

    [Fact]
    public void Complete_WhenMissionIsNotStarted_Throws()
    {
        var mission = CreateAssignedMission();

        Assert.Throws<InvalidOperationException>(() => mission.Complete(60));
        Assert.Equal(MissionStatus.Assigned, mission.Status);
    }

    [Theory]
    [InlineData(1, 500)]
    [InlineData(31, 1000)]
    [InlineData(90, 1500)]
    public void Complete_WhenMissionIsStarted_CalculatesFinalTotal(int actualDurationMinutes, int expectedAmount)
    {
        var mission = CreateAssignedMission();
        mission.Start(ProviderId, CompanyId);

        mission.Complete(actualDurationMinutes);

        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Equal(expectedAmount, mission.FinalTotalAmount);
        Assert.Equal(actualDurationMinutes, mission.ActualDurationMinutes);
    }

    [Theory]
    [InlineData(MissionStatus.Completed)]
    [InlineData(MissionStatus.Cancelled)]
    [InlineData(MissionStatus.Disputed)]
    public void Assign_WhenMissionIsClosed_Throws(MissionStatus status)
    {
        var mission = CreateMission(60);
        SetProperty(mission, nameof(Mission.Status), status);

        Assert.Throws<InvalidOperationException>(() => mission.Assign(ProviderId, CompanyId, 1000));
    }

    [Fact]
    public void MarkProviderAccepted_WhenAssigned_MovesMissionToAcceptedWithoutReleasingContacts()
    {
        var mission = CreateAssignedMission();

        mission.MarkProviderAccepted(ProviderId, CompanyId);

        Assert.Equal(MissionStatus.Accepted, mission.Status);
        Assert.NotNull(mission.ProviderAcceptedAt);
        Assert.False(mission.CanRevealContactDetails);
        Assert.Null(mission.ContactDetailsReleasedAt);
    }

    [Fact]
    public void ConfirmByCustomer_WhenProviderAccepted_AuthorizesPaymentAndReleasesContacts()
    {
        var mission = CreateAssignedMission();
        mission.MarkProviderAccepted(ProviderId, CompanyId);

        mission.ConfirmByCustomer(platformCommissionAmount: 1200, transportFeeAmount: 800);

        Assert.Equal(PaymentStatus.Authorized, mission.PaymentStatus);
        Assert.Equal(1200, mission.PlatformCommissionAmount);
        Assert.Equal(800, mission.TransportFeeAmount);
        Assert.NotNull(mission.CustomerConfirmedAt);
        Assert.NotNull(mission.ContactDetailsReleasedAt);
        Assert.True(mission.CanRevealContactDetails);
    }

    [Fact]
    public void ConfirmByCustomer_WhenProviderHasNotAccepted_ThrowsAndKeepsContactsHidden()
    {
        var mission = CreateAssignedMission();

        Assert.Throws<InvalidOperationException>(() => mission.ConfirmByCustomer(1200, 800));
        Assert.False(mission.CanRevealContactDetails);
        Assert.Equal(PaymentStatus.Pending, mission.PaymentStatus);
    }

    [Fact]
    public void CancelByCustomer_AfterContactRelease_KeepsCancellationFee()
    {
        var mission = CreateAssignedMission();
        mission.MarkProviderAccepted(ProviderId, CompanyId);
        mission.ConfirmByCustomer(platformCommissionAmount: 1200, transportFeeAmount: 800);

        mission.CancelByCustomer(2500);

        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal(2500, mission.CancellationFeeAmount);
        Assert.False(mission.CanRevealContactDetails);
    }

    [Fact]
    public void CancelByCustomer_BeforeContactRelease_DoesNotChargeCancellationFee()
    {
        var mission = CreateAssignedMission();

        mission.CancelByCustomer(2500);

        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal(0, mission.CancellationFeeAmount);
    }

    private static Mission CreateAssignedMission()
    {
        var mission = CreateMission(90);
        mission.Assign(ProviderId, CompanyId, 1000);
        return mission;
    }

    private static Mission CreateMission(int durationMinutes)
    {
        return new Mission(CustomerId, ServiceId, MissionMode.Instant, PaymentMethod.MobileMoney, null, durationMinutes);
    }

    private static void SetProperty<T>(object instance, string propertyName, T value)
    {
        instance.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }
}
