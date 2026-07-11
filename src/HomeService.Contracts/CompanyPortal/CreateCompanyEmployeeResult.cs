namespace HomeService.Contracts.CompanyPortal;

public sealed record CreateCompanyEmployeeResult(
    Guid Id,
    string? InvitationCode = null,
    string? InvitationLink = null,
    DateTimeOffset? InvitationExpiresAt = null);
