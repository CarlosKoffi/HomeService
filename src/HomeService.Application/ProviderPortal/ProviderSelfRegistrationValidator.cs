using HomeService.Contracts.ProviderPortal;

namespace HomeService.Application.ProviderPortal;

public static class ProviderSelfRegistrationValidator
{
    public static IReadOnlyList<string> Validate(
        ProviderSelfRegistrationRequest request,
        int resolvedServiceCount,
        int selectedOpportunityCount)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors.Add("Renseignez le prenom.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors.Add("Renseignez le nom.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            errors.Add("Renseignez le numero de telephone.");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            errors.Add("Renseignez votre adresse ou commune.");
        }

        if (string.IsNullOrWhiteSpace(request.Gender))
        {
            errors.Add("Selectionnez le sexe du prestataire.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            errors.Add("Le mot de passe doit contenir au moins 8 caracteres.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            errors.Add("Les deux mots de passe ne correspondent pas.");
        }

        if (resolvedServiceCount == 0)
        {
            errors.Add("Selectionnez ou proposez au moins un service.");
        }

        if (selectedOpportunityCount == 0)
        {
            errors.Add("Selectionnez au moins une entreprise proche pour envoyer votre demande.");
        }

        return errors;
    }
}
