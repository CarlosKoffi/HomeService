using HomeService.Application.Missions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;

namespace HomeService.Tests.Unit.Domain;

public sealed class QualityControlTests
{
    [Fact]
    public void Required_confirmation_is_complete_only_when_confirmed()
    {
        var template = new QualityChecklistTemplate(Guid.NewGuid(), Guid.NewGuid(), "Controle", null);
        var source = new QualityChecklistItem(template.Id, "need-confirmed", "Besoin confirme", QualityChecklistStage.BeforeStart, QualityChecklistResponseType.Confirmation, true, 10);
        var item = new MissionQualityItem(Guid.NewGuid(), source);

        item.Respond(false, null, null, null);
        Assert.False(item.IsCompleted);

        item.Respond(true, null, null, null);
        Assert.True(item.IsCompleted);
        Assert.NotNull(item.CompletedAt);
    }

    [Fact]
    public void Photo_control_requires_a_real_attachment_reference()
    {
        var template = new QualityChecklistTemplate(Guid.NewGuid(), Guid.NewGuid(), "Controle", null);
        var source = new QualityChecklistItem(template.Id, "final-photo", "Photo finale", QualityChecklistStage.BeforeCompletion, QualityChecklistResponseType.Photo, true, 10);
        var item = new MissionQualityItem(Guid.NewGuid(), source);

        item.Respond(true, null, null, null);
        Assert.False(item.IsCompleted);

        item.Respond(true, null, null, Guid.NewGuid());
        Assert.True(item.IsCompleted);
    }

    [Fact]
    public void Expired_qualification_is_not_eligible()
    {
        var qualification = new ProviderPrestationQualification(Guid.NewGuid(), Guid.NewGuid());
        qualification.Review(ProviderQualificationStatus.Approved, 85, 90, null, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(qualification.IsEligible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Quality_score_moves_the_best_company_up_in_dispatch()
    {
        var scoring = new MissionDispatchScoringService();
        var request = new MissionDispatchRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Abidjan", false, 2);
        var lowerQuality = Candidate("Entreprise A", 55);
        var higherQuality = Candidate("Entreprise B", 95);

        var result = scoring.SelectTopCompanies(request, [lowerQuality, higherQuality]);

        Assert.Equal(higherQuality.CompanyId, result[0].CompanyId);
    }

    private static MissionDispatchCandidate Candidate(string name, int qualityScore) => new(
        Guid.NewGuid(), name, 1, true, true, 4.5m, 10, 0, 0, 0, 0m, qualityScore);
}
