using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Application.Companies;

public static class CompanyAssignmentModePresenter
{
    public static CompanyAssignmentModeResponse ToResponse(Company company)
    {
        var additionalCommissionRate = company.AssignmentMode == CompanyAssignmentMode.PlatformManaged ? 0.05m : 0m;
        var message = company.AssignmentMode == CompanyAssignmentMode.PlatformManaged
            ? "La plateforme affecte les missions. Une commission additionnelle pourra etre appliquee."
            : "L'entreprise affecte elle-meme les missions depuis son portail.";

        return new CompanyAssignmentModeResponse(
            company.Id,
            company.AssignmentMode.ToString(),
            additionalCommissionRate,
            message);
    }
}
