namespace HomeService.Contracts.Companies;

public sealed record CompanyActivationPasswordResponse(
    bool IsSuccess,
    string Message);
