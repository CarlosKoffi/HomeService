using HomeService.Contracts.Companies;

namespace HomeService.Application.Companies;

public static class CompanyActivationPasswordValidator
{
    public static string? Validate(CompanyActivationPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return "Le token d'activation est obligatoire.";
        }

        if (request.Password.Length < 10)
        {
            return "Le mot de passe doit contenir au moins 10 caracteres.";
        }

        if (!request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsLower) || !request.Password.Any(char.IsDigit))
        {
            return "Le mot de passe doit contenir une majuscule, une minuscule et un chiffre.";
        }

        if (request.Password != request.ConfirmPassword)
        {
            return "Les deux mots de passe ne correspondent pas.";
        }

        return null;
    }
}
