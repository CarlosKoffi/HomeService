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
    public void Constructor_GeneratesReadableUniqueMissionNumber()
    {
        var firstMission = CreateMission(60);
        var secondMission = CreateMission(60);

        Assert.StartsWith("MIS-", firstMission.MissionNumber);
        Assert.Matches(@"^MIS-\d{6}-[A-F0-9]{8}$", firstMission.MissionNumber);
        Assert.NotEqual(firstMission.MissionNumber, secondMission.MissionNumber);
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
    [InlineData(MissionStatus.Resolved)]
    public void Assign_WhenMissionIsClosed_Throws(MissionStatus status)
    {
        var mission = CreateMission(60);
        SetProperty(mission, nameof(Mission.Status), status);

        Assert.Throws<InvalidOperationException>(() => mission.Assign(ProviderId, CompanyId, 1000));
    }

    [Fact]
    public void AssignWithCompanyQuote_WhenQuoteIsInsideRange_StoresQuoteAndWaitsForCustomer()
    {
        var mission = CreateMission(60);

        mission.AssignWithCompanyQuote(
            ProviderId,
            CompanyId,
            quotedAmount: 7500,
            maxAllowedAmount: 10000,
            overMaxJustification: null,
            partsEstimateAmount: 2000,
            partsDescription: "Joint et flexible",
            assignmentSource: MissionAssignmentSource.Kaza,
            isInterimProvider: true);

        Assert.Equal(ProviderId, mission.ProviderId);
        Assert.Equal(CompanyId, mission.CompanyId);
        Assert.Equal(7500, mission.CompanyQuotedAmount);
        Assert.Equal(7500, mission.EstimatedTotalAmount);
        Assert.Null(mission.HourlyRateAmount);
        Assert.Null(mission.CompanyQuoteJustification);
        Assert.Equal(2000, mission.PartsEstimateAmount);
        Assert.Equal("Joint et flexible", mission.PartsDescription);
        Assert.Equal(MissionAssignmentSource.Kaza, mission.AssignmentSource);
        Assert.True(mission.IsInterimProviderSnapshot);
        Assert.Equal(MissionQuoteStatus.Submitted, mission.QuoteStatus);
        Assert.NotNull(mission.CompanyQuotedAt);
        Assert.Equal(MissionStatus.Assigned, mission.Status);
    }

    [Fact]
    public void AssignWithCompanyQuote_WhenQuoteExceedsRangeWithoutJustification_Throws()
    {
        var mission = CreateMission(60);

        Assert.Throws<InvalidOperationException>(() =>
            mission.AssignWithCompanyQuote(ProviderId, CompanyId, quotedAmount: 12500, maxAllowedAmount: 10000, overMaxJustification: " "));
    }

    [Fact]
    public void AssignWithCompanyQuote_WhenQuoteExceedsRangeWithJustification_StoresJustification()
    {
        var mission = CreateMission(60);

        mission.AssignWithCompanyQuote(ProviderId, CompanyId, quotedAmount: 12500, maxAllowedAmount: 10000, overMaxJustification: " Piece rare a remplacer ");

        Assert.Equal(12500, mission.CompanyQuotedAmount);
        Assert.Equal("Piece rare a remplacer", mission.CompanyQuoteJustification);
    }

    [Fact]
    public void AcceptCompanyQuote_WhenAssignedWithQuote_StoresCustomerAcceptance()
    {
        var mission = CreateMission(60);
        mission.AssignWithCompanyQuote(ProviderId, CompanyId, quotedAmount: 7500, maxAllowedAmount: 10000, overMaxJustification: null);

        mission.AcceptCompanyQuote();

        Assert.NotNull(mission.CustomerQuoteAcceptedAt);
        Assert.Equal(MissionQuoteStatus.Accepted, mission.QuoteStatus);
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

        mission.ConfirmByCustomer(
            platformCommissionAmount: 1200,
            transportFeeAmount: 800,
            platformCommissionRateBasisPoints: 1500,
            kazaAssignmentCommissionRateBasisPoints: 500);

        Assert.Equal(PaymentStatus.Authorized, mission.PaymentStatus);
        Assert.Equal(1200, mission.PlatformCommissionAmount);
        Assert.Equal(1500, mission.PlatformCommissionRateBasisPoints);
        Assert.Equal(500, mission.KazaAssignmentCommissionRateBasisPoints);
        Assert.Equal(300, mission.CompanyPayoutAmount);
        Assert.Equal(800, mission.TransportFeeAmount);
        Assert.NotNull(mission.CustomerConfirmedAt);
        Assert.NotNull(mission.ContactDetailsReleasedAt);
        Assert.True(mission.CanRevealContactDetails);
    }

    [Fact]
    public void Complete_WhenMissionHasCompanyQuote_KeepsQuotedAmountAsFinalTotal()
    {
        var mission = CreateMission(90);
        mission.AssignWithCompanyQuote(ProviderId, CompanyId, quotedAmount: 9000, maxAllowedAmount: 10000, overMaxJustification: null);
        mission.MarkProviderAccepted(ProviderId, CompanyId);
        mission.ConfirmByCustomer(platformCommissionAmount: 1800, transportFeeAmount: 0, platformCommissionRateBasisPoints: 2000);
        mission.Start(ProviderId, CompanyId);

        mission.Complete(actualDurationMinutes: 30);

        Assert.Equal(9000, mission.FinalTotalAmount);
        Assert.Equal(7200, mission.CompanyPayoutAmount);
    }

    [Fact]
    public void UpdateCustomerRequest_WhenQuoteRequired_MarksQuoteAsRequested()
    {
        var mission = CreateMission(60);

        mission.UpdateCustomerRequest(" Robinet qui fuit ", requiresCompanyQuote: true);

        Assert.Equal("Robinet qui fuit", mission.Description);
        Assert.True(mission.RequiresCompanyQuote);
        Assert.Equal(MissionQuoteStatus.Requested, mission.QuoteStatus);
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

    [Fact]
    public void MarkDisputed_WhenMissionIsOpen_MovesToDispute()
    {
        var mission = CreateAssignedMission();

        mission.MarkDisputed();

        Assert.Equal(MissionStatus.Disputed, mission.Status);
        Assert.NotNull(mission.UpdatedAt);
    }

    [Theory]
    [InlineData(MissionStatus.Completed)]
    [InlineData(MissionStatus.Cancelled)]
    [InlineData(MissionStatus.Resolved)]
    public void MarkDisputed_WhenMissionIsClosed_Throws(MissionStatus status)
    {
        var mission = CreateMission(60);
        SetProperty(mission, nameof(Mission.Status), status);

        Assert.Throws<InvalidOperationException>(mission.MarkDisputed);
    }

    [Fact]
    public void ResolveDispute_WhenMissionIsDisputed_MarksResolved()
    {
        var mission = CreateAssignedMission();
        mission.MarkDisputed();

        mission.ResolveDispute();

        Assert.Equal(MissionStatus.Resolved, mission.Status);
        Assert.NotNull(mission.UpdatedAt);
    }

    [Fact]
    public void ResolveDispute_WhenMissionIsNotDisputed_Throws()
    {
        var mission = CreateAssignedMission();

        Assert.Throws<InvalidOperationException>(mission.ResolveDispute);
        Assert.Equal(MissionStatus.Assigned, mission.Status);
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
