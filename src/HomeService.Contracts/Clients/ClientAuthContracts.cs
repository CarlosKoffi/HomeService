namespace HomeService.Contracts.Clients;

public sealed record RegisterClientRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    string Password,
    bool RememberMe = true);

public sealed record LoginClientRequest(
    string PhoneNumber,
    string Password,
    bool RememberMe = true);

public sealed record ClientAuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid CustomerId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email);

public sealed record ClientMeResponse(
    Guid CustomerId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    string? ProfilePhotoUrl);

public sealed record ClientProfilePhotoResponse(string ProfilePhotoUrl);

public sealed record UpdateClientProfileRequest(
    string FirstName,
    string LastName,
    string? Email);
