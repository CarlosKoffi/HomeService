namespace HomeService.Contracts.Companies;

public sealed record CompanyActivationPasswordRequest(
    string Token,
    string Password,
    string ConfirmPassword);
