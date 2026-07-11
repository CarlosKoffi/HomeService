namespace HomeService.Domain.Enums;

public enum LocationVerificationStatus
{
    NotChecked = 0,
    Verified = 1,
    OutsideTolerance = 2,
    MissingProviderLocation = 3,
    MissingMissionLocation = 4,
    LowAccuracy = 5,
    InvalidProviderLocation = 6
}
