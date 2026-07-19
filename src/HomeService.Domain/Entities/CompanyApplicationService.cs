using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class CompanyApplicationService : AuditableEntity
{
    private CompanyApplicationService()
    {
    }

    public CompanyApplicationService(Guid companyApplicationId, string rawName)
    {
        CompanyApplicationId = companyApplicationId;
        RawName = rawName.Trim();
        NormalizedName = Normalize(rawName);
    }

    public Guid CompanyApplicationId { get; private set; }
    public CompanyApplication? CompanyApplication { get; private set; }
    public Guid? MatchedServiceId { get; private set; }
    public Service? MatchedService { get; private set; }
    public Guid? MatchedServicePrestationId { get; private set; }
    public ServicePrestation? MatchedServicePrestation { get; private set; }
    public string RawName { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int? MatchScore { get; private set; }
    public CompanyApplicationServiceMatchStatus MatchStatus { get; private set; } = CompanyApplicationServiceMatchStatus.PendingMatch;
    public string? ReviewNote { get; private set; }

    public void MarkAsMatched(Guid serviceId, int score)
    {
        MatchedServiceId = serviceId;
        MatchedServicePrestationId = null;
        MatchScore = score;
        MatchStatus = CompanyApplicationServiceMatchStatus.MatchedExisting;
        Touch();
    }

    public void MarkAsMatchedPrestation(Guid serviceId, Guid servicePrestationId, int score)
    {
        MatchedServiceId = serviceId;
        MatchedServicePrestationId = servicePrestationId;
        MatchScore = score;
        MatchStatus = CompanyApplicationServiceMatchStatus.MatchedExisting;
        Touch();
    }

    public void MarkForReview(int? score = null)
    {
        MatchScore = score;
        MatchStatus = CompanyApplicationServiceMatchStatus.NeedsAdminReview;
        Touch();
    }

    public void MarkCreatedAsNewService(Guid serviceId)
    {
        MatchedServiceId = serviceId;
        MatchedServicePrestationId = null;
        MatchStatus = CompanyApplicationServiceMatchStatus.CreatedAsNewService;
        Touch();
    }

    private static string Normalize(string value)
    {
        return CatalogNameNormalizer.Normalize(value);
    }
}
