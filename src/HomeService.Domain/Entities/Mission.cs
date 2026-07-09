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
    public string Currency { get; private set; } = "XOF";

    public void Assign(Guid providerId, Guid companyId, int hourlyRateAmount)
    {
        ProviderId = providerId;
        CompanyId = companyId;
        HourlyRateAmount = hourlyRateAmount;
        EstimatedTotalAmount = CalculateAmount(EstimatedDurationMinutes, hourlyRateAmount);
        Status = MissionStatus.Assigned;
        Touch();
    }

    public void Complete(int actualDurationMinutes)
    {
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
