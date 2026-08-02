namespace HomeService.Client.Mobile.Services;

public static class PaymentProviderLogoResolver
{
    public static async Task<ImageSource> ResolveAsync(
        ClientMobileApiClient apiClient,
        string? code,
        string? name,
        string method,
        string? remoteUrl,
        CancellationToken cancellationToken = default)
    {
        var remote = await apiClient.DownloadMediaImageSourceAsync(remoteUrl, cancellationToken);
        return remote ?? ImageSource.FromFile(ResolveBundledAsset(code, name, method));
    }

    private static string ResolveBundledAsset(string? code, string? name, string method)
    {
        var identity = $"{code} {name}".ToLowerInvariant();
        if (identity.Contains("orange")) return "payment_orange_money.png";
        if (identity.Contains("mtn") || identity.Contains("momo")) return "payment_mtn_momo.png";
        if (identity.Contains("moov")) return "payment_moov_money.png";
        if (identity.Contains("wave")) return "payment_wave.png";
        if (method.Equals("Card", StringComparison.OrdinalIgnoreCase)) return "payment_bank_card.png";
        return "profile_payment.svg";
    }
}
