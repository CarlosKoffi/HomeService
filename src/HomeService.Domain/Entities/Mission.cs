using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class Mission : AuditableEntity
{
    private Mission()
    {
    }

    public Mission(
        Guid customerId,
        Guid serviceId,
        MissionMode mode,
        PaymentMethod paymentMethod,
        DateTimeOffset? scheduledFor,
        int estimatedDurationMinutes)
    {
        CustomerId = customerId;
        ServiceId = serviceId;
        Mode = mode;
        PaymentMethod = paymentMethod;
        ScheduledFor = scheduledFor;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    public Guid CustomerId { get; private set; }
    public Guid ServiceId { get; private set; }
    public Guid? ProviderId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public MissionMode Mode { get; private set; }
    public MissionStatus Status { get; private set; } = MissionStatus.Created;
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;
    public DateTimeOffset? ScheduledFor { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public int? ActualDurationMinutes { get; private set; }
    public int? HourlyRateAmount { get; private set; }
    public int? EstimatedTotalAmount { get; private set; }
    public int? FinalTotalAmount { get; private set; }
    public int? CompanyQuotedAmount { get; private set; }
    public string? CompanyQuoteJustification { get; private set; }
    public DateTimeOffset? CompanyQuotedAt { get; private set; }
    public DateTimeOffset? CustomerQuoteAcceptedAt { get; private set; }
    public int PlatformCommissionAmount { get; private set; }
    public int TransportFeeAmount { get; private set; }
    public int CancellationFeeAmount { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string? ServiceAddress { get; private set; }
    public decimal? ServiceLatitude { get; private set; }
    public decimal? ServiceLongitude { get; private set; }
    public int ArrivalToleranceMeters { get; private set; } = 250;
    public DateTimeOffset? ProviderAcceptedAt { get; private set; }
    public DateTimeOffset? CustomerConfirmedAt { get; private set; }
    public DateTimeOffset? ContactDetailsReleasedAt { get; private set; }
    public bool CanRevealContactDetails => ContactDetailsReleasedAt is not null
        && Status is MissionStatus.Accepted or MissionStatus.OnTheWay or MissionStatus.Started or MissionStatus.Completed
        && PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid;

    public void SetServiceLocation(string? address, decimal? latitude, decimal? longitude, int arrivalToleranceMeters = 250)
    {
        ServiceAddress = address?.Trim();
        ServiceLatitude = latitude;
        ServiceLongitude = longitude;
        ArrivalToleranceMeters = Math.Clamp(arrivalToleranceMeters, 50, 2000);
        Touch();
    }

    public void Assign(Guid providerId, Guid companyId, int hourlyRateAmount)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed)
        {
            throw new InvalidOperationException("Mission cannot be assigned in its current state.");
        }

        ProviderId = providerId;
        CompanyId = companyId;
        HourlyRateAmount = hourlyRateAmount;
        EstimatedTotalAmount = CalculateAmount(EstimatedDurationMinutes, hourlyRateAmount);
        Status = MissionStatus.Assigned;
        Touch();
    }

    public void AssignWithCompanyQuote(
        Guid providerId,
        Guid companyId,
        int quotedAmount,
        int maxAllowedAmount,
        string? overMaxJustification)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed)
        {
            throw new InvalidOperationException("Mission cannot be assigned in its current state.");
        }

        var normalizedQuote = Math.Max(0, quotedAmount);
        if (normalizedQuote > Math.Max(0, maxAllowedAmount) && string.IsNullOrWhiteSpace(overMaxJustification))
        {
            throw new InvalidOperationException("A justification is required when the quoted amount exceeds the configured maximum.");
        }

        ProviderId = providerId;
        CompanyId = companyId;
        CompanyQuotedAmount = normalizedQuote;
        CompanyQuoteJustification = string.IsNullOrWhiteSpace(overMaxJustification)
            ? null
            : overMaxJustification.Trim();
        CompanyQuotedAt = DateTimeOffset.UtcNow;
        EstimatedTotalAmount = normalizedQuote;
        HourlyRateAmount = null;
        CustomerQuoteAcceptedAt = null;
        Status = MissionStatus.Assigned;
        Touch();
    }

    public void AcceptCompanyQuote()
    {
        if (CompanyQuotedAmount is null)
        {
            throw new InvalidOperationException("Mission has no company quote to accept.");
        }

        if (Status != MissionStatus.Assigned)
        {
            throw new InvalidOperationException("Only assigned missions can have their quote accepted.");
        }

        CustomerQuoteAcceptedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void MarkProviderAccepted(Guid providerId, Guid companyId)
    {
        if (Status is not (MissionStatus.Assigned or MissionStatus.Offered))
        {
            throw new InvalidOperationException("Only assigned or offered missions can be accepted by a provider.");
        }

        if (ProviderId is not null && ProviderId != providerId)
        {
            throw new InvalidOperationException("Mission is assigned to another provider.");
        }

        if (CompanyId is not null && CompanyId != companyId)
        {
            throw new InvalidOperationException("Mission is assigned to another company.");
        }

        ProviderId = providerId;
        CompanyId = companyId;
        Status = MissionStatus.Accepted;
        ProviderAcceptedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void ConfirmByCustomer(int platformCommissionAmount, int transportFeeAmount)
    {
        if (Status != MissionStatus.Accepted)
        {
            throw new InvalidOperationException("Only provider accepted missions can be confirmed by the customer.");
        }

        PlatformCommissionAmount = Math.Max(0, platformCommissionAmount);
        TransportFeeAmount = Math.Max(0, transportFeeAmount);
        PaymentStatus = PaymentStatus.Authorized;
        CustomerConfirmedAt = DateTimeOffset.UtcNow;
        ContactDetailsReleasedAt = CustomerConfirmedAt;
        Touch();
    }

    public void CancelByCustomer(int cancellationFeeAmount)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled)
        {
            throw new InvalidOperationException("Mission cannot be cancelled in its current state.");
        }

        CancellationFeeAmount = ContactDetailsReleasedAt is null ? 0 : Math.Max(0, cancellationFeeAmount);
        Status = MissionStatus.Cancelled;
        Touch();
    }

    public void MarkDisputed()
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled)
        {
            throw new InvalidOperationException("Completed or cancelled missions cannot be marked as disputed.");
        }

        Status = MissionStatus.Disputed;
        Touch();
    }

    public bool CanStartFor(Guid providerId, Guid companyId)
    {
        if (ProviderId is not null && ProviderId != providerId)
        {
            return false;
        }

        if (CompanyId is not null && CompanyId != companyId)
        {
            return false;
        }

        return Status is MissionStatus.Accepted or MissionStatus.Assigned or MissionStatus.OnTheWay;
    }

    public void Start(Guid providerId, Guid companyId)
    {
        if (!CanStartFor(providerId, companyId))
        {
            throw new InvalidOperationException("Mission cannot be started in its current state.");
        }

        ProviderId = providerId;
        CompanyId = companyId;
        Status = MissionStatus.Started;
        Touch();
    }

    public void Complete(int actualDurationMinutes)
    {
        if (Status != MissionStatus.Started)
        {
            throw new InvalidOperationException("Only started missions can be completed.");
        }

        ActualDurationMinutes = actualDurationMinutes;
        FinalTotalAmount = CalculateAmount(actualDurationMinutes, HourlyRateAmount ?? 0);
        Status = MissionStatus.Completed;
        Touch();
    }

    private static int CalculateAmount(int durationMinutes, int hourlyRateAmount)
    {
        var billableHalfHours = (int)Math.Ceiling(durationMinutes / 30m);
        return billableHalfHours * hourlyRateAmount / 2;
    }
}
