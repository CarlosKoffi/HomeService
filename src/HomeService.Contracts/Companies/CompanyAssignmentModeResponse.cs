namespace HomeService.Contracts.Companies;

public sealed record CompanyAssignmentModeResponse(
    Guid CompanyId,
    string AssignmentMode,
    decimal AdditionalCommissionRate,
    string Message);
