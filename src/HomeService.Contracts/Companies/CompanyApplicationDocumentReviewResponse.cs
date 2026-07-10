namespace HomeService.Contracts.Companies;

public sealed record CompanyApplicationDocumentReviewResponse(
    Guid Id,
    Guid CompanyApplicationId,
    string ReviewStatus,
    string? ReviewNote);
