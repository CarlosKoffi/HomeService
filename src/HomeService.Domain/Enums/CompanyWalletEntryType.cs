namespace HomeService.Domain.Enums;

public enum CompanyWalletEntryType
{
    MissionCreditPending = 1,
    FundsBecameAvailable = 2,
    PayoutReserved = 3,
    PayoutPaid = 4,
    PayoutFailed = 5,
    ManualAdjustment = 6
}
