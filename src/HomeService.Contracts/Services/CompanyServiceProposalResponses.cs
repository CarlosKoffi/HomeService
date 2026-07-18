namespace HomeService.Contracts.Services;

public sealed record CompanyServiceProposalResponse(
    Guid Id,
    Guid CompanyApplicationId,
    Guid? CompanyId,
    string CompanyName,
    string RawName,
    string NormalizedName,
    string MatchStatus,
    int? MatchScore,
    Guid? MatchedServiceId,
    string? MatchedServiceName,
    Guid? MatchedServicePrestationId,
    string? MatchedServicePrestationName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CompanyServiceProposalSuggestionResponse> Suggestions);

public sealed record CompanyServiceProposalSuggestionResponse(
    Guid ServiceId,
    string ServiceName,
    Guid? ServicePrestationId,
    string? ServicePrestationName,
    string Kind,
    int Score);

public sealed record CompanyServiceProposalListResponse(
    IReadOnlyList<CompanyServiceProposalResponse> Items,
    int TotalPending);

public sealed record AttachCompanyServiceProposalRequest(
    Guid? ServiceId,
    Guid? ServicePrestationId,
    string? Note = null);

public sealed record CreatePrestationFromCompanyServiceProposalRequest(
    Guid ServiceId,
    string? Name = null,
    string? Description = null,
    int SortOrder = 0,
    string? Note = null);
