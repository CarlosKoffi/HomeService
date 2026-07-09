namespace HomeService.Domain.Enums;

public enum MissionStatus
{
    Created = 0,
    SearchingProvider = 1,
    Offered = 2,
    Accepted = 3,
    Assigned = 4,
    OnTheWay = 5,
    Started = 6,
    Completed = 7,
    Cancelled = 8,
    Disputed = 9
}
