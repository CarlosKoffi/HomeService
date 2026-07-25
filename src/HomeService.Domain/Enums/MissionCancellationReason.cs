namespace HomeService.Domain.Enums;

public enum MissionCancellationReason
{
    CustomerChangedMind = 0,
    CustomerUnavailable = 1,
    CustomerAbsent = 2,
    AccessRefused = 3,
    ProviderUnavailable = 4,
    ProviderNoShow = 5,
    CompanyUnavailable = 6,
    AssignmentExpired = 7,
    DuplicateRequest = 8,
    Other = 99
}
