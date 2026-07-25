namespace HomeService.Contracts.Clients;

public sealed record CreateClientMissionRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    Guid ServiceId,
    Guid? ServicePrestationId,
    string Mode,
    string PaymentMethod,
    DateTimeOffset? ScheduledFor,
    int EstimatedDurationMinutes,
    string? Description,
    string? ServiceAddress,
    decimal? ServiceLatitude,
    decimal? ServiceLongitude,
    bool RequiresCompanyQuote,
    bool IsUrgent);
