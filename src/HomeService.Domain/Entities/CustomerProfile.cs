using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CustomerProfile : AuditableEntity
{
    private CustomerProfile()
    {
    }

    public CustomerProfile(string firstName, string lastName, string phoneNumber)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber.Trim();
    }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public string? ProfilePhotoPath { get; private set; }

    public void UpdateProfile(string firstName, string lastName, string? email)
    {
        FirstName = CleanRequired(firstName, 120);
        LastName = CleanRequired(lastName, 120);
        Email = Clean(email, 180)?.ToLowerInvariant();
        Touch();
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = CleanRequired(passwordHash, 512);
        Touch();
    }

    public void SetProfilePhotoPath(string profilePhotoPath)
    {
        ProfilePhotoPath = CleanRequired(profilePhotoPath, 500);
        Touch();
    }

    private static string CleanRequired(string value, int maxLength)
    {
        var cleaned = Clean(value, maxLength);
        if (cleaned is null)
        {
            throw new ArgumentException("La valeur obligatoire est vide.", nameof(value));
        }

        return cleaned;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
