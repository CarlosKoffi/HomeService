namespace HomeService.Application.Notifications;

public interface IMobilePushSender
{
    Task<MobilePushSendResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken);
}

public sealed record MobilePushSendResult(
    bool IsSuccess,
    string? ProviderMessageId,
    string? ErrorMessage)
{
    public static MobilePushSendResult Sent(string? providerMessageId)
        => new(true, providerMessageId, null);

    public static MobilePushSendResult Failed(string message)
        => new(false, null, message);
}
