using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientAuthService(IAppDbContext db)
{
    public async Task<ClientAuthResult> RegisterAsync(
        RegisterClientRequest request,
        CancellationToken cancellationToken,
        CustomerAccountType accountType = CustomerAccountType.Personal)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return ClientAuthResult.Invalid(errors);
        }

        var phone = NormalizePhone(request.PhoneNumber);
        var existing = await db.Customers.FirstOrDefaultAsync(
            customer => customer.PhoneNumber == phone && customer.AccountType == accountType,
            cancellationToken);
        if (existing is not null)
        {
            return ClientAuthResult.Invalid(["Ce numero est deja rattache a un compte client."]);
        }

        var customer = new CustomerProfile(request.FirstName, request.LastName, phone, accountType);
        customer.UpdateProfile(request.FirstName, request.LastName, request.Email);
        customer.SetPasswordHash(Sha256PasswordHasher.Hash(request.Password));
        db.Customers.Add(customer);

        var session = CreateSession(customer.Id, request.RememberMe);
        db.CustomerSessions.Add(session.Session);
        await db.SaveChangesAsync(cancellationToken);

        return ClientAuthResult.Ok(ToAuthResponse(customer, session.Token, session.Session.ExpiresAt));
    }

    public async Task<ClientAuthResult> LoginAsync(
        LoginClientRequest request,
        CancellationToken cancellationToken,
        CustomerAccountType accountType = CustomerAccountType.Personal)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ClientAuthResult.Invalid(["Numero et mot de passe obligatoires."]);
        }

        var phone = NormalizePhone(request.PhoneNumber);
        var customer = await db.Customers.FirstOrDefaultAsync(
            item => item.PhoneNumber == phone && item.AccountType == accountType,
            cancellationToken);
        if (customer is null
            || string.IsNullOrWhiteSpace(customer.PasswordHash)
            || !Sha256PasswordHasher.Verify(request.Password, customer.PasswordHash))
        {
            return ClientAuthResult.Invalid(["Identifiants client invalides."]);
        }

        if (Sha256PasswordHasher.NeedsRehash(customer.PasswordHash))
        {
            customer.SetPasswordHash(Sha256PasswordHasher.Hash(request.Password));
        }

        var session = CreateSession(customer.Id, request.RememberMe);
        db.CustomerSessions.Add(session.Session);
        await db.SaveChangesAsync(cancellationToken);

        return ClientAuthResult.Ok(ToAuthResponse(customer, session.Token, session.Session.ExpiresAt));
    }

    public async Task<CustomerProfile?> GetSessionCustomerAsync(
        string? authorizationHeader,
        CancellationToken cancellationToken,
        CustomerAccountType? expectedAccountType = null)
    {
        var token = ExtractBearerToken(authorizationHeader);
        if (token is null)
        {
            return null;
        }

        var tokenHash = PortalTokenService.HashToken(token);
        var session = await db.CustomerSessions
            .Include(item => item.Customer)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash && item.RevokedAt == null && item.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);

        return session?.Customer is { } customer
            && (expectedAccountType is null || customer.AccountType == expectedAccountType)
                ? customer
                : null;
    }

    public async Task<bool> LogoutAsync(string? authorizationHeader, CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(authorizationHeader);
        if (token is null)
        {
            return false;
        }

        var tokenHash = PortalTokenService.HashToken(token);
        var session = await db.CustomerSessions.FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);
        if (session is null)
        {
            return false;
        }

        session.Revoke();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static string NormalizePhone(string phoneNumber)
    {
        return new string(phoneNumber.Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray()).Trim();
    }

    private static List<string> ValidateRegistration(RegisterClientRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors.Add("Prenom obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors.Add("Nom obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            errors.Add("Numero de telephone obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors.Add("Le mot de passe doit contenir au moins 8 caracteres.");
        }

        return errors;
    }

    private static (string Token, CustomerSession Session) CreateSession(Guid customerId, bool rememberMe)
    {
        var token = PortalTokenService.GenerateSecureToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(rememberMe ? 30 : 1);
        return (token, new CustomerSession(customerId, PortalTokenService.HashToken(token), expiresAt));
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string prefix = "Bearer ";
        return authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : null;
    }

    private static ClientAuthResponse ToAuthResponse(CustomerProfile customer, string token, DateTimeOffset expiresAt)
    {
        return new ClientAuthResponse(
            token,
            expiresAt,
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.PhoneNumber,
            customer.Email);
    }
}

public sealed record ClientAuthResult(
    bool IsSuccess,
    ClientAuthResponse? Response,
    IReadOnlyList<string> Errors)
{
    public static ClientAuthResult Ok(ClientAuthResponse response)
        => new(true, response, []);

    public static ClientAuthResult Invalid(IReadOnlyList<string> errors)
        => new(false, null, errors);
}
