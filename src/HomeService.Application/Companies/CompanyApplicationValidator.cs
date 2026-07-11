using System.Text.RegularExpressions;
using HomeService.Contracts.Companies;

namespace HomeService.Application.Companies;

public static class CompanyApplicationValidator
{
    public static IReadOnlyList<string> Validate(RegisterCompanyRequest request)
    {
        var errors = new List<string>();

        if (request.CompanyName.Trim().Length < 3)
        {
            errors.Add("Le nom legal de l'entreprise doit contenir au moins 3 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(request.RegistrationNumber) && request.RegistrationNumber.Trim().Length < 4)
        {
            errors.Add("Le numero legal semble trop court.");
        }

        if (request.City.Trim().Length < 2)
        {
            errors.Add("La ville est obligatoire.");
        }

        if (request.ContactName.Trim().Length < 3)
        {
            errors.Add("Le nom du responsable est obligatoire.");
        }

        if (!Regex.IsMatch(request.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
        {
            errors.Add("L'email professionnel n'est pas valide.");
        }

        var phoneDigits = Regex.Replace(request.PhoneNumber, @"\D", string.Empty);
        if (phoneDigits.Length is < 8 or > 15)
        {
            errors.Add("Le telephone doit contenir entre 8 et 15 chiffres.");
        }

        if (request.EstimatedProviderCount is not null and (< 1 or > 10000))
        {
            errors.Add("Le nombre de prestataires doit etre compris entre 1 et 10000.");
        }

        if (request.Services.Count == 0)
        {
            errors.Add("Au moins un service est requis.");
        }

        if (request.Password.Length < 10)
        {
            errors.Add("Le mot de passe doit contenir au moins 10 caracteres.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            errors.Add("Les deux mots de passe ne correspondent pas.");
        }

        return errors;
    }
}
