using HomeService.Domain.Common;
using HomeService.Domain.Enums;

namespace HomeService.Domain.Entities;

public sealed class ProviderMissionAssignment : AuditableEntity
{
    private ProviderMissionAssignment()
    {
    }

    public ProviderMissionAssignment(Guid missionId, Guid providerId, Guid companyId, DateTimeOffset expiresAt)
    {
        MissionId = missionId;
        ProviderId = providerId;
        CompanyId = companyId;
        ExpiresAt = expiresAt;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public ProviderMissionAssignmentStatus Status { get; private set; } = ProviderMissionAssignmentStatus.Offered;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public ProviderMissionRefusalReason? RefusalReason { get; private set; }
    public string? RefusalComment { get; private set; }
    public string? CompletionNote { get; private set; }
    public string? CompletionPhotoPath { get; private set; }
    public decimal? OfferedLatitude { get; private set; }
    public decimal? OfferedLongitude { get; private set; }
    public int? OfferedAccuracyMeters { get; private set; }
    public decimal? AcceptedLatitude { get; private set; }
    public decimal? AcceptedLongitude { get; private set; }
    public int? AcceptedAccuracyMeters { get; private set; }
    public decimal? ArrivalLatitude { get; private set; }
    public decimal? ArrivalLongitude { get; private set; }
    public int? ArrivalAccuracyMeters { get; private set; }
    public int? ArrivalDistanceMeters { get; private set; }
    public int ArrivalToleranceMeters { get; private set; } = 250;
    public LocationVerificationStatus ArrivalVerificationStatus { get; private set; } = LocationVerificationStatus.NotChecked;
    public DateTimeOffset? ArrivalVerifiedAt { get; private set; }
    public bool HasVerifiedArrival => ArrivalVerificationStatus == LocationVerificationStatus.Verified;

    public void CaptureOfferLocation(decimal? latitude, decimal? longitude, int? accuracyMeters)
    {
        OfferedLatitude = latitude;
        OfferedLongitude = longitude;
        OfferedAccuracyMeters = accuracyMeters;
        Touch();
    }

    public void Accept(decimal? latitude = null, decimal? longitude = null, int? accuracyMeters = null)
    {
        if (Status != ProviderMissionAssignmentStatus.Offered)
        {
            throw new InvalidOperationException("Only offered assignments can be accepted.");
        }

        if (ExpiresAt <= DateTimeOffset.UtcNow)
        {
            MarkExpired();
            throw new InvalidOperationException("Expired assignments cannot be accepted.");
        }

        Status = ProviderMissionAssignmentStatus.Accepted;
        RespondedAt = DateTimeOffset.UtcNow;
        AcceptedLatitude = latitude;
        AcceptedLongitude = longitude;
        AcceptedAccuracyMeters = accuracyMeters;
        Touch();
    }

    public void Refuse(ProviderMissionRefusalReason reason, string? comment)
    {
        if (Status != ProviderMissionAssignmentStatus.Offered)
        {
            throw new InvalidOperationException("Only offered assignments can be refused.");
        }

        Status = ProviderMissionAssignmentStatus.Refused;
        RespondedAt = DateTimeOffset.UtcNow;
        RefusalReason = reason;
        RefusalComment = comment?.Trim();
        Touch();
    }

    public void MarkExpired()
    {
        Status = ProviderMissionAssignmentStatus.Expired;
        Touch();
    }

    public void VerifyArrival(
        decimal? providerLatitude,
        decimal? providerLongitude,
        int? accuracyMeters,
        decimal? missionLatitude,
        decimal? missionLongitude,
        int toleranceMeters)
    {
        ArrivalLatitude = providerLatitude;
        ArrivalLongitude = providerLongitude;
        ArrivalAccuracyMeters = accuracyMeters;
        ArrivalToleranceMeters = Math.Clamp(toleranceMeters, 50, 2000);
        ArrivalVerifiedAt = DateTimeOffset.UtcNow;

        if (providerLatitude is null || providerLongitude is null)
        {
            ArrivalVerificationStatus = LocationVerificationStatus.MissingProviderLocation;
            ArrivalDistanceMeters = null;
            Touch();
            return;
        }

        if (providerLatitude is < -90 or > 90 || providerLongitude is < -180 or > 180)
        {
            ArrivalVerificationStatus = LocationVerificationStatus.InvalidProviderLocation;
            ArrivalDistanceMeters = null;
            Touch();
            return;
        }

        if (missionLatitude is null || missionLongitude is null)
        {
            ArrivalVerificationStatus = LocationVerificationStatus.MissingMissionLocation;
            ArrivalDistanceMeters = null;
            Touch();
            return;
        }

        ArrivalDistanceMeters = CalculateDistanceMeters(
            (double)providerLatitude.Value,
            (double)providerLongitude.Value,
            (double)missionLatitude.Value,
            (double)missionLongitude.Value);

        if (accuracyMeters is null or <= 0 or > 150)
        {
            ArrivalVerificationStatus = LocationVerificationStatus.LowAccuracy;
            Touch();
            return;
        }

        ArrivalVerificationStatus = ArrivalDistanceMeters <= ArrivalToleranceMeters
            ? LocationVerificationStatus.Verified
            : LocationVerificationStatus.OutsideTolerance;

        Touch();
    }

    public void Start()
    {
        if (Status != ProviderMissionAssignmentStatus.Accepted)
        {
            throw new InvalidOperationException("Only accepted assignments can be started.");
        }

        if (!HasVerifiedArrival)
        {
            throw new InvalidOperationException("Arrival must be verified before starting the assignment.");
        }

        Status = ProviderMissionAssignmentStatus.Started;
        StartedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Complete(string? note, string? completionPhotoPath)
    {
        if (Status != ProviderMissionAssignmentStatus.Started)
        {
            throw new InvalidOperationException("Only started assignments can be completed.");
        }

        Status = ProviderMissionAssignmentStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        CompletionNote = note?.Trim();
        CompletionPhotoPath = completionPhotoPath;
        Touch();
    }

    private static int CalculateDistanceMeters(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        const double earthRadiusMeters = 6371000;
        var latA = DegreesToRadians(latitudeA);
        var latB = DegreesToRadians(latitudeB);
        var deltaLatitude = DegreesToRadians(latitudeB - latitudeA);
        var deltaLongitude = DegreesToRadians(longitudeB - longitudeA);

        var haversine = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2)
            + Math.Cos(latA) * Math.Cos(latB) * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return (int)Math.Round(earthRadiusMeters * centralAngle);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
