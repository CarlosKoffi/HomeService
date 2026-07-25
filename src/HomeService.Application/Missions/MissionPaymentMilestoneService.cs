using HomeService.Application.Abstractions;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Missions;

public sealed class MissionPaymentMilestoneService(IAppDbContext db)
{
    public async Task EnsureMissionStartedMilestoneAsync(Mission mission, CancellationToken cancellationToken)
    {
        await EnsureMilestoneAsync(
            mission.Id,
            MissionPaymentMilestoneTrigger.MissionStarted,
            0,
            mission.Currency,
            "Mission demarree - fonds client bloques",
            10,
            markDue: true,
            cancellationToken);
    }

    public async Task EnsureMissionCompletedMilestoneAsync(Mission mission, CancellationToken cancellationToken)
    {
        await EnsureMilestoneAsync(
            mission.Id,
            MissionPaymentMilestoneTrigger.MissionCompleted,
            mission.CompanyPayoutAmount,
            mission.Currency,
            "Mission terminee - paiement entreprise a liberer",
            20,
            markDue: true,
            cancellationToken);
    }

    private async Task EnsureMilestoneAsync(
        Guid missionId,
        MissionPaymentMilestoneTrigger trigger,
        int amount,
        string currency,
        string label,
        int sortOrder,
        bool markDue,
        CancellationToken cancellationToken)
    {
        var alreadyTracked = db.MissionPaymentMilestones.Local
            .Any(item => item.MissionId == missionId && item.Trigger == trigger);
        if (alreadyTracked)
        {
            return;
        }

        var exists = await db.MissionPaymentMilestones
            .AnyAsync(item => item.MissionId == missionId && item.Trigger == trigger, cancellationToken);
        if (exists)
        {
            return;
        }

        var milestone = new MissionPaymentMilestone(missionId, trigger, amount, currency, label, sortOrder);
        if (markDue)
        {
            milestone.MarkDue(DateTimeOffset.UtcNow);
        }

        db.MissionPaymentMilestones.Add(milestone);
    }
}
