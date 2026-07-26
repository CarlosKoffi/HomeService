namespace HomeService.Infrastructure.Notifications;

public sealed class FirebaseOptions
{
    public bool Enabled { get; set; }
    public string? ProjectId { get; set; }
    public string? CredentialsJson { get; set; }
}
