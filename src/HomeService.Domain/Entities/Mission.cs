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
        int estimatedDurationMinutes,
        Guid? servicePrestationId = null,
        string? description = null,
        bool requiresCompanyQuote = false,
        Guid? serviceOptionId = null)
    {
        CustomerId = customerId;
        ServiceId = serviceId;
        MissionNumber = GenerateMissionNumber();
        ServicePrestationId = servicePrestationId;
        ServiceOptionId = serviceOptionId;
        Mode = mode;
        PaymentMethod = paymentMethod;
        ScheduledFor = scheduledFor;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        Description = Clean(description);
        RequiresCompanyQuote = requiresCompanyQuote;
        QuoteStatus = requiresCompanyQuote ? MissionQuoteStatus.Requested : MissionQuoteStatus.NotRequired;
    }

    public Guid CustomerId { get; private set; }
    public Guid ServiceId { get; private set; }
    public string MissionNumber { get; private set; } = string.Empty;
    public Guid? ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public Guid? ServiceOptionId { get; private set; }
    public ServiceOption? ServiceOption { get; private set; }
    public Guid? ProviderId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public MissionMode Mode { get; private set; }
    public MissionStatus Status { get; private set; } = MissionStatus.Created;
    public string? Description { get; private set; }
    public bool RequiresCompanyQuote { get; private set; }
    public MissionQuoteStatus QuoteStatus { get; private set; } = MissionQuoteStatus.NotRequired;
    public PaymentMethod PaymentMethod { get; private set; }
    public Guid? CustomerPaymentMethodId { get; private set; }
    public CustomerPaymentMethod? CustomerPaymentMethod { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;
    public DateTimeOffset? ScheduledFor { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public int? ActualDurationMinutes { get; private set; }
    public int? HourlyRateAmount { get; private set; }
    public int? EstimatedTotalAmount { get; private set; }
    public int? FinalTotalAmount { get; private set; }
    public int? CompanyQuotedAmount { get; private set; }
    public string? CompanyQuoteJustification { get; private set; }
    public int? PartsEstimateAmount { get; private set; }
    public string? PartsDescription { get; private set; }
    public DateTimeOffset? CompanyQuotedAt { get; private set; }
    public DateTimeOffset? CustomerQuoteAcceptedAt { get; private set; }
    public int PlatformCommissionAmount { get; private set; }
    public int PlatformCommissionRateBasisPoints { get; private set; }
    public int KazaAssignmentCommissionRateBasisPoints { get; private set; }
    public int CompanyPayoutAmount { get; private set; }
    public int TransportFeeAmount { get; private set; }
    public int CancellationFeeAmount { get; private set; }
    public MissionAssignmentSource AssignmentSource { get; private set; } = MissionAssignmentSource.Company;
    public bool IsInterimProviderSnapshot { get; private set; }
    public string Currency { get; private set; } = "XOF";
    public string? ServiceAddress { get; private set; }
    public decimal? ServiceLatitude { get; private set; }
    public decimal? ServiceLongitude { get; private set; }
    public int ArrivalToleranceMeters { get; private set; } = 250;
    public DateTimeOffset? ProviderAcceptedAt { get; private set; }
    public DateTimeOffset? CustomerPaymentExpiresAt { get; private set; }
    public DateTimeOffset? CustomerConfirmedAt { get; private set; }
    public DateTimeOffset? CustomerCompletionValidatedAt { get; private set; }
    public DateTimeOffset? CustomerCompletionValidationExpiresAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public MissionCancellationActor? CancelledBy { get; private set; }
    public MissionCancellationReason? CancellationReason { get; private set; }
    public string? CancellationComment { get; private set; }
    public int RefundAmount { get; private set; }
    public DateTimeOffset? ContactDetailsReleasedAt { get; private set; }
    public DateTimeOffset? CompanyPayoutReleasedAt { get; private set; }
    public DateTimeOffset? CompanyAssignmentExpiresAt { get; private set; }
    public int DispatchRound { get; private set; }
    public bool CanRevealContactDetails => ContactDetailsReleasedAt is not null
        && Status is MissionStatus.Accepted or MissionStatus.OnTheWay or MissionStatus.Started or MissionStatus.Completed
        && PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid;
    public bool IsInitialPaymentConfirmed => CustomerConfirmedAt is not null
        && PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid;

    public void SelectCustomerPaymentMethod(CustomerPaymentMethod paymentMethod)
    {
        if (paymentMethod.CustomerId != CustomerId || !paymentMethod.IsActive)
        {
            throw new InvalidOperationException("Le moyen de paiement ne peut pas etre utilise pour cette mission.");
        }

        CustomerPaymentMethodId = paymentMethod.Id;
        PaymentMethod = paymentMethod.Method;
        Touch();
    }

    public void SetServiceLocation(string? address, decimal? latitude, decimal? longitude, int arrivalToleranceMeters = 250)
    {
        ServiceAddress = address?.Trim();
        ServiceLatitude = latitude;
        ServiceLongitude = longitude;
        ArrivalToleranceMeters = Math.Clamp(arrivalToleranceMeters, 50, 2000);
        Touch();
    }

    public void UpdateCustomerRequest(string? description, bool requiresCompanyQuote)
    {
        Description = Clean(description);
        RequiresCompanyQuote = requiresCompanyQuote;
        QuoteStatus = requiresCompanyQuote ? MissionQuoteStatus.Requested : MissionQuoteStatus.NotRequired;
        Touch();
    }

    public void StartCompanySearch()
    {
        if (Status != MissionStatus.Created)
        {
            throw new InvalidOperationException("Only newly created missions can start company search.");
        }

        Status = MissionStatus.SearchingProvider;
        Touch();
    }

    public void MarkCompanyOffersSent()
    {
        if (Status is not (MissionStatus.Created or MissionStatus.SearchingProvider or MissionStatus.Offered))
        {
            throw new InvalidOperationException("Mission cannot be offered to companies in its current state.");
        }

        Status = MissionStatus.Offered;
        DispatchRound++;
        Touch();
    }

    public void AcceptCompanyOffer(Guid companyId, DateTimeOffset assignmentExpiresAt)
    {
        if (Status is not (MissionStatus.Offered or MissionStatus.SearchingProvider))
        {
            throw new InvalidOperationException("Mission cannot be accepted by a company in its current state.");
        }

        if (CompanyId is not null && CompanyId != companyId)
        {
            throw new InvalidOperationException("Mission is already owned by another company.");
        }

        CompanyId = companyId;
        Status = MissionStatus.SearchingProvider;
        CompanyAssignmentExpiresAt = assignmentExpiresAt;
        Touch();
    }

    public void ReleaseCompanyAfterAssignmentTimeout(DateTimeOffset now)
    {
        if (CompanyId is null || ProviderId is not null || CompanyAssignmentExpiresAt is null || CompanyAssignmentExpiresAt > now)
        {
            return;
        }

        CompanyId = null;
        CompanyAssignmentExpiresAt = null;
        Status = MissionStatus.Offered;
        Touch();
    }

    public void Assign(Guid providerId, Guid companyId, int hourlyRateAmount)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed or MissionStatus.Resolved)
        {
            throw new InvalidOperationException("Mission cannot be assigned in its current state.");
        }

        ProviderId = providerId;
        CompanyId = companyId;
        CompanyAssignmentExpiresAt = null;
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
        string? overMaxJustification,
        int? partsEstimateAmount = null,
        string? partsDescription = null,
        MissionAssignmentSource assignmentSource = MissionAssignmentSource.Company,
        bool isInterimProvider = false)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed or MissionStatus.Resolved)
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
        CompanyAssignmentExpiresAt = null;
        AssignmentSource = assignmentSource;
        IsInterimProviderSnapshot = isInterimProvider;
        CompanyQuotedAmount = normalizedQuote;
        CompanyQuoteJustification = string.IsNullOrWhiteSpace(overMaxJustification)
            ? null
            : overMaxJustification.Trim();
        PartsEstimateAmount = partsEstimateAmount.HasValue ? Math.Max(0, partsEstimateAmount.Value) : null;
        PartsDescription = Clean(partsDescription);
        CompanyQuotedAt = DateTimeOffset.UtcNow;
        EstimatedTotalAmount = normalizedQuote;
        HourlyRateAmount = null;
        CustomerQuoteAcceptedAt = null;
        QuoteStatus = MissionQuoteStatus.Submitted;
        Status = MissionStatus.Assigned;
        Touch();
    }

    public void AcceptCompanyQuote()
    {
        if (CompanyQuotedAmount is null)
        {
            throw new InvalidOperationException("Mission has no company quote to accept.");
        }

        if (Status is not (MissionStatus.Assigned or MissionStatus.Accepted))
        {
            throw new InvalidOperationException("Only assigned or provider accepted missions can have their quote accepted.");
        }

        CustomerQuoteAcceptedAt = DateTimeOffset.UtcNow;
        QuoteStatus = MissionQuoteStatus.Accepted;
        Touch();
    }

    public void MarkProviderAccepted(
        Guid providerId,
        Guid companyId,
        DateTimeOffset? customerPaymentExpiresAt = null)
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
        CustomerPaymentExpiresAt = customerPaymentExpiresAt;
        Touch();
    }

    public void MarkProviderOnTheWay(Guid providerId, Guid companyId)
    {
        if (Status == MissionStatus.OnTheWay)
        {
            return;
        }

        if (!IsInitialPaymentConfirmed)
        {
            throw new InvalidOperationException("Customer payment must be confirmed before provider tracking starts.");
        }

        if (Status != MissionStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted missions can be marked as on the way.");
        }

        if (ProviderId != providerId || CompanyId != companyId)
        {
            throw new InvalidOperationException("Mission does not belong to this provider assignment.");
        }

        Status = MissionStatus.OnTheWay;
        Touch();
    }

    public void ReleaseProviderAfterRefusal(Guid providerId)
    {
        if (ProviderId != providerId)
        {
            return;
        }

        if (Status is not MissionStatus.Assigned)
        {
            return;
        }

        ProviderId = null;
        ProviderAcceptedAt = null;
        Status = MissionStatus.SearchingProvider;
        Touch();
    }

    public void ResetDispatchAfterProviderAcceptanceTimeout(Guid providerId)
    {
        if (ProviderId != providerId || Status is not MissionStatus.Assigned)
        {
            return;
        }

        ProviderId = null;
        CompanyId = null;
        CompanyAssignmentExpiresAt = null;
        ProviderAcceptedAt = null;
        Status = MissionStatus.Offered;
        Touch();
    }

    public void ResetStalledDispatch()
    {
        if (Status is not MissionStatus.SearchingProvider || ProviderId is not null || CompanyId is null)
        {
            return;
        }

        CompanyId = null;
        CompanyAssignmentExpiresAt = null;
        Status = MissionStatus.Offered;
        Touch();
    }

    public void ConfirmByCustomer(
        int platformCommissionAmount,
        int transportFeeAmount,
        int platformCommissionRateBasisPoints = 0,
        int kazaAssignmentCommissionRateBasisPoints = 0)
    {
        if (Status != MissionStatus.Accepted)
        {
            throw new InvalidOperationException("Only provider accepted missions can be confirmed by the customer.");
        }

        PlatformCommissionAmount = Math.Max(0, platformCommissionAmount);
        PlatformCommissionRateBasisPoints = Math.Clamp(platformCommissionRateBasisPoints, 0, 10000);
        KazaAssignmentCommissionRateBasisPoints = Math.Clamp(kazaAssignmentCommissionRateBasisPoints, 0, 10000);
        TransportFeeAmount = Math.Max(0, transportFeeAmount);
        CompanyPayoutAmount = Math.Max(0, (CompanyQuotedAmount ?? EstimatedTotalAmount ?? FinalTotalAmount ?? 0) - PlatformCommissionAmount);
        PaymentStatus = PaymentStatus.Authorized;
        CustomerConfirmedAt = DateTimeOffset.UtcNow;
        CustomerPaymentExpiresAt = null;
        ContactDetailsReleasedAt = CustomerConfirmedAt;
        Touch();
    }

    public void CancelByCustomer(int cancellationFeeAmount)
    {
        Cancel(
            MissionCancellationActor.Customer,
            MissionCancellationReason.Other,
            null,
            cancellationFeeAmount,
            CalculateRefundAmount(cancellationFeeAmount));
    }

    public void Cancel(
        MissionCancellationActor cancelledBy,
        MissionCancellationReason reason,
        string? comment,
        int cancellationFeeAmount,
        int refundAmount)
    {
        if (Status is MissionStatus.Started or MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Disputed or MissionStatus.Resolved)
        {
            throw new InvalidOperationException("A mission cannot be cancelled once the intervention has started.");
        }

        CancellationFeeAmount = ContactDetailsReleasedAt is null ? 0 : Math.Max(0, cancellationFeeAmount);
        RefundAmount = Math.Max(0, refundAmount);
        CancelledAt = DateTimeOffset.UtcNow;
        CancelledBy = cancelledBy;
        CancellationReason = reason;
        CancellationComment = Clean(comment);
        if (PaymentStatus == PaymentStatus.Authorized)
        {
            PaymentStatus = RefundAmount > 0 ? PaymentStatus.Refunded : PaymentStatus.Failed;
        }

        Status = MissionStatus.Cancelled;
        Touch();
    }

    public void MarkDisputed()
    {
        if (Status is MissionStatus.Cancelled or MissionStatus.Resolved)
        {
            throw new InvalidOperationException("Cancelled or resolved missions cannot be marked as disputed.");
        }

        Status = MissionStatus.Disputed;
        Touch();
    }

    public void ResolveDispute()
    {
        if (Status != MissionStatus.Disputed)
        {
            throw new InvalidOperationException("Only disputed missions can be resolved.");
        }

        Status = MissionStatus.Resolved;
        Touch();
    }

    public void ApplyDisputeRefund(int refundAmount)
    {
        if (refundAmount < 0)
        {
            throw new InvalidOperationException("Refund amount cannot be negative.");
        }

        RefundAmount = refundAmount;
        if (RefundAmount > 0 && PaymentStatus is PaymentStatus.Authorized or PaymentStatus.Paid)
        {
            PaymentStatus = PaymentStatus.Refunded;
        }

        Touch();
    }

    public bool CanStartFor(Guid providerId, Guid companyId)
    {
        if (!IsInitialPaymentConfirmed)
        {
            return false;
        }

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

    public void Complete(
        int actualDurationMinutes,
        DateTimeOffset? customerCompletionValidationExpiresAt = null)
    {
        if (Status != MissionStatus.Started)
        {
            throw new InvalidOperationException("Only started missions can be completed.");
        }

        ActualDurationMinutes = actualDurationMinutes;
        FinalTotalAmount = CompanyQuotedAmount ?? CalculateAmount(actualDurationMinutes, HourlyRateAmount ?? 0);
        CompanyPayoutAmount = Math.Max(0, FinalTotalAmount.Value - PlatformCommissionAmount);
        Status = MissionStatus.Completed;
        CustomerCompletionValidationExpiresAt = customerCompletionValidationExpiresAt;
        Touch();
    }

    public void ValidateCompletionByCustomer(DateTimeOffset? validatedAt = null)
    {
        if (Status != MissionStatus.Completed)
        {
            throw new InvalidOperationException("Only completed missions can be validated by the customer.");
        }

        if (CustomerCompletionValidatedAt is not null)
        {
            return;
        }

        CustomerCompletionValidatedAt = validatedAt ?? DateTimeOffset.UtcNow;
        CustomerCompletionValidationExpiresAt = null;
        CompanyPayoutReleasedAt = CustomerCompletionValidatedAt;
        PaymentStatus = PaymentStatus.Paid;
        Touch();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int CalculateAmount(int durationMinutes, int hourlyRateAmount)
    {
        var billableHalfHours = (int)Math.Ceiling(durationMinutes / 30m);
        return billableHalfHours * hourlyRateAmount / 2;
    }

    private int CalculateRefundAmount(int cancellationFeeAmount)
    {
        if (PaymentStatus is not (PaymentStatus.Authorized or PaymentStatus.Paid))
        {
            return 0;
        }

        var paidAmount = CompanyQuotedAmount ?? EstimatedTotalAmount ?? FinalTotalAmount ?? 0;
        return Math.Max(0, paidAmount - Math.Max(0, cancellationFeeAmount));
    }

    private static string GenerateMissionNumber()
    {
        return $"MIS-{DateTimeOffset.UtcNow:yyMMdd}-{Guid.NewGuid():N}"[..19].ToUpperInvariant();
    }
}
