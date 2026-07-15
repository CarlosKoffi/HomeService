using System.Text.RegularExpressions;
using System.Net.Mail;

namespace HomeService.Application.CompanyPortal;

public static class CompanyProviderValidator
{
    public static IReadOnlyList<string> Validate(CompanyProviderFormData provider)
    {
        var errors = new List<string>();

        if (provider.FirstName.Trim().Length < 2)
        {
            errors.Add("Le prenom de l'employe est obligatoire.");
        }

        if (provider.LastName.Trim().Length < 2)
        {
            errors.Add("Le nom de l'employe est obligatoire.");
        }

        var phoneDigits = Regex.Replace(provider.PhoneNumber, @"\D", string.Empty);
        if (phoneDigits.Length is < 8 or > 15)
        {
            errors.Add("Le telephone de l'employe doit contenir entre 8 et 15 chiffres.");
        }

        if (!string.IsNullOrWhiteSpace(provider.Email) && !IsValidEmail(provider.Email))
        {
            errors.Add("L'email de l'employe doit etre au bon format.");
        }

        if (provider.DateOfBirth is null)
        {
            errors.Add("La date de naissance est obligatoire.");
        }
        else if (provider.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)))
        {
            errors.Add("L'employe doit avoir au moins 16 ans.");
        }

        if (provider.Address.Trim().Length < 4)
        {
            errors.Add("L'adresse de l'employe est obligatoire.");
        }

        if (provider.YearsOfExperience is null or < 0 or > 60)
        {
            errors.Add("Le nombre d'annees d'experience doit etre compris entre 0 et 60.");
        }

        if (provider.MissionRadiusKm is null or < 1 or > 100)
        {
            errors.Add("Le perimetre de mission doit etre compris entre 1 et 100 km.");
        }

        if (provider.ServiceIds.Count == 0)
        {
            errors.Add("Au moins un service maitrise est obligatoire.");
        }

        if (!provider.HasPhoto)
        {
            errors.Add("La photo de l'employe est obligatoire.");
        }

        if (!provider.HasIdentityDocument)
        {
            errors.Add("La piece d'identite de l'employe est obligatoire.");
        }

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
