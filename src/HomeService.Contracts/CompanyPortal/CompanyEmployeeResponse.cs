namespace HomeService.Contracts.CompanyPortal;

public sealed record CompanyEmployeeResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    DateOnly? BirthDate,
    string? Address,
    string Gender,
    string EmploymentType,
    bool ReceivesDirectRequests,
    int YearsOfExperience,
    string Status,
    bool IsAvailable,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    decimal? ServiceRadiusKm,
    string? PhotoUrl,
    string? IdentityDocumentUrl,
    string? DiplomaDocumentUrl,
    bool HasDiploma,
    IReadOnlyList<CompanyEmployeeServiceResponse> Services,
    IReadOnlyList<CompanyEmployeeDocumentResponse> Documents,
    int CompletedMissionCount,
    CompanyEmployeeCurrentMissionResponse? CurrentMission,
    DateTimeOffset CreatedAt,
    string? InvitationCode = null,
    string? InvitationLink = null,
    DateTimeOffset? InvitationExpiresAt = null);

public sealed record CompanyEmployeeCurrentMissionResponse(
    Guid MissionId,
    string ServiceName,
    string CustomerName,
    string? LocationLabel,
    DateTimeOffset? ScheduledFor,
    string Status);
