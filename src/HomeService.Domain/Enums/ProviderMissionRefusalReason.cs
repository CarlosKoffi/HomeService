namespace HomeService.Domain.Enums;

public enum ProviderMissionRefusalReason
{
    TooFar = 0,
    Unavailable = 1,
    MissingEquipment = 2,
    ClientUnreachable = 3,
    AlreadyBusy = 4,
    Other = 99
}
