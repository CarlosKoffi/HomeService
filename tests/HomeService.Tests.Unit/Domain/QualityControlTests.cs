using HomeService.Application.Missions;
using HomeService.Application.Quality;
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
    public void Editing_a_template_item_does_not_change_the_mission_snapshot()
    {
        var template = new QualityChecklistTemplate(Guid.NewGuid(), Guid.NewGuid(), "Controle", null);
        var source = new QualityChecklistItem(template.Id, "initial-check", "Consigne initiale", QualityChecklistStage.DuringMission, QualityChecklistResponseType.Confirmation, true, 10);
        var missionItem = new MissionQualityItem(Guid.NewGuid(), source);

        source.Update(
            "Nouvelle consigne",
            "Nouvelle aide",
            QualityChecklistStage.BeforeCompletion,
            QualityChecklistResponseType.ShortText,
            true,
            false,
            20);

        Assert.Equal("Consigne initiale", missionItem.Label);
        Assert.Equal(QualityChecklistStage.DuringMission, missionItem.Stage);
        Assert.Equal(QualityChecklistResponseType.Confirmation, missionItem.ResponseType);
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

    [Fact]
    public void Mission_can_be_completed_when_half_of_required_controls_are_done()
    {
        var result = MissionQualityChecklistService.EvaluateCompletionGate(
            completedRequiredItemCount: 4,
            requiredItemCount: 8,
            exceptionReason: null,
            missingItems: ["Photo finale"]);

        Assert.True(result.IsAllowed);
        Assert.False(result.UsedException);
        Assert.Equal(50, result.CompletionPercentage);
    }

    [Fact]
    public void Mission_is_blocked_below_half_without_a_detailed_reason()
    {
        var result = MissionQualityChecklistService.EvaluateCompletionGate(
            completedRequiredItemCount: 3,
            requiredItemCount: 8,
            exceptionReason: "Pas possible",
            missingItems: ["Photo finale", "Zone nettoyee"]);

        Assert.False(result.IsAllowed);
        Assert.Equal(37, result.CompletionPercentage);
        Assert.Contains("50 %", result.Message);
        Assert.Equal(2, result.MissingItems.Count);
    }

    [Fact]
    public void Detailed_exception_allows_completion_below_half_and_is_flagged()
    {
        var result = MissionQualityChecklistService.EvaluateCompletionGate(
            completedRequiredItemCount: 2,
            requiredItemCount: 8,
            exceptionReason: "Le client a refuse les photos dans son logement.",
            missingItems: ["Photo initiale", "Photo finale"]);

        Assert.True(result.IsAllowed);
        Assert.True(result.UsedException);
        Assert.Equal(25, result.CompletionPercentage);
    }

    private static MissionDispatchCandidate Candidate(string name, int qualityScore) => new(
        Guid.NewGuid(), name, 1, true, true, 4.5m, 10, 0, 0, 0, 0m, qualityScore);
}
