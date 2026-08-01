namespace HomeService.Application.Admin;

public static class AdminBootstrapPasswordPolicy
{
    public static bool ShouldSetPassword(string? currentPasswordHash, bool forcePasswordReset)
        => forcePasswordReset || string.IsNullOrWhiteSpace(currentPasswordHash);
}
