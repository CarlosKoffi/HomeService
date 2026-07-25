using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class MissionDispatchOffer : AuditableEntity
{
    private MissionDispatchOffer()
    {
    }

    public MissionDispatchOffer(
        Guid missionId,
        Guid companyId,
        int rank,
        int score,
        string scoreDetails,
        DateTimeOffset expiresAt)
    {
        MissionId = missionId;
        CompanyId = companyId;
        Rank = Math.Max(1, rank);
        Score = Math.Max(0, score);
        ScoreDetails = CleanRequired(scoreDetails);
        ExpiresAt = expiresAt;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public int Rank { get; private set; }
    public int Score { get; private set; }
    public string ScoreDetails { get; private set; } = string.Empty;
    public MissionDispatchOfferStatus Status { get; private set; } = MissionDispatchOfferStatus.Sent;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }

    public bool IsOpen(DateTimeOffset now)
    {
        return Status == MissionDispatchOfferStatus.Sent && ExpiresAt > now;
    }

    public void Accept(DateTimeOffset now)
    {
        if (!IsOpen(now))
        {
            throw new InvalidOperationException("Cette offre de mission n'est plus disponible.");
        }

        Status = MissionDispatchOfferStatus.Accepted;
        RespondedAt = now;
        Touch();
    }

    public void MarkLost()
    {
        if (Status != MissionDispatchOfferStatus.Sent)
        {
            return;
        }

        Status = MissionDispatchOfferStatus.Lost;
        Touch();
    }

    public void MarkExpired(DateTimeOffset now)
    {
        if (Status != MissionDispatchOfferStatus.Sent || ExpiresAt > now)
        {
            return;
        }

        Status = MissionDispatchOfferStatus.Expired;
        Touch();
    }

    public void Cancel()
    {
        if (Status is MissionDispatchOfferStatus.Accepted or MissionDispatchOfferStatus.Cancelled)
        {
            return;
        }

        Status = MissionDispatchOfferStatus.Cancelled;
        Touch();
    }

    private static string CleanRequired(string value)
    {
        var cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (cleaned is null)
        {
            throw new ArgumentException("Le detail du score est obligatoire.", nameof(value));
        }

        return cleaned;
    }
}
