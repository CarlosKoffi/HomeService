using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using HomeService.Contracts.Services;

namespace HomeService.Mobile.Shared;

public sealed class CatalogMediaResolver(HttpClient httpClient)
{
    private const int MaxCachedImages = 96;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(8);
    private readonly SemaphoreSlim catalogGate = new(1, 1);
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> imageCache = new(StringComparer.OrdinalIgnoreCase);
    private CatalogIndex? catalog;

    public async Task<ImageSource?> ResolveServiceAsync(
        Guid? serviceId,
        string? serviceName,
        CancellationToken cancellationToken = default)
    {
        var index = await GetCatalogAsync(cancellationToken);
        var service = index.FindService(serviceId, serviceName);
        return await DownloadAsync(service?.IconUrl ?? service?.ImageUrl, cancellationToken);
    }

    public async Task<ImageSource?> ResolvePrestationAsync(
        Guid? prestationId,
        string? prestationName,
        Guid? serviceId = null,
        string? serviceName = null,
        CancellationToken cancellationToken = default)
    {
        var index = await GetCatalogAsync(cancellationToken);
        var service = index.FindService(serviceId, serviceName);
        var prestation = index.FindPrestation(prestationId, prestationName, service);
        return await DownloadAsync(
            prestation?.IllustrationUrl ?? service?.IconUrl ?? service?.ImageUrl,
            cancellationToken);
    }

    private async Task<CatalogIndex> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (catalog is not null) return catalog;
        await catalogGate.WaitAsync(cancellationToken);
        try
        {
            if (catalog is not null) return catalog;
            try
            {
                using var response = await httpClient.GetAsync(
                    "api/services",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (!response.IsSuccessStatusCode) return CatalogIndex.Empty;
                var services = await response.Content.ReadFromJsonAsync<IReadOnlyList<ServiceSummaryResponse>>(
                    cancellationToken: cancellationToken) ?? [];
                catalog = new CatalogIndex(services);
                return catalog;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
            {
                return CatalogIndex.Empty;
            }
        }
        finally
        {
            catalogGate.Release();
        }
    }

    private async Task<ImageSource?> DownloadAsync(string? mediaUrl, CancellationToken cancellationToken)
    {
        var absoluteUrl = ResolveAbsoluteUrl(mediaUrl);
        if (absoluteUrl is null) return null;

        if (imageCache.Count >= MaxCachedImages && !imageCache.ContainsKey(absoluteUrl))
        {
            var evictedKey = imageCache.Keys.FirstOrDefault();
            if (evictedKey is not null) imageCache.TryRemove(evictedKey, out _);
        }

        try
        {
            var lazyBytes = imageCache.GetOrAdd(
                absoluteUrl,
                key => new Lazy<Task<byte[]?>>(
                    () => DownloadBytesAsync(key),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            var bytes = await lazyBytes.Value.WaitAsync(cancellationToken);
            return bytes is null || bytes.Length == 0
                ? null
                : ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<byte[]?> DownloadBytesAsync(string mediaUrl)
    {
        var candidates = BuildCandidates(mediaUrl);
        foreach (var candidate in candidates)
        {
            try
            {
                using var timeout = new CancellationTokenSource(DownloadTimeout);
                using var response = await httpClient.GetAsync(
                    candidate,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                if (!response.IsSuccessStatusCode) continue;
                var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
                if (bytes.Length > 0) return bytes;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
            {
                // Le proxy API ci-dessous prend le relais si le CDN est temporairement inaccessible.
            }
        }

        imageCache.TryRemove(mediaUrl, out _);
        return null;
    }

    private IReadOnlyList<Uri> BuildCandidates(string mediaUrl)
    {
        if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var directUri)) return [];
        var candidates = new List<Uri> { directUri };
        if (httpClient.BaseAddress is not null
            && directUri.Host.Equals("media.wele.africa", StringComparison.OrdinalIgnoreCase)
            && (directUri.AbsolutePath.StartsWith("/assets/services/", StringComparison.OrdinalIgnoreCase)
                || directUri.AbsolutePath.StartsWith("/catalog/prestations/", StringComparison.OrdinalIgnoreCase)))
        {
            var builder = new UriBuilder(new Uri(httpClient.BaseAddress, directUri.AbsolutePath.TrimStart('/')));
            var query = directUri.Query.TrimStart('?');
            builder.Query = string.IsNullOrWhiteSpace(query) ? "proxy=1" : $"{query}&proxy=1";
            candidates.Add(builder.Uri);
        }

        return candidates;
    }

    private string? ResolveAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)) return absolute.ToString();
        return httpClient.BaseAddress is null
            ? null
            : new Uri(httpClient.BaseAddress, url.TrimStart('/')).ToString();
    }

    private sealed class CatalogIndex(IReadOnlyList<ServiceSummaryResponse> services)
    {
        public static CatalogIndex Empty { get; } = new([]);

        public ServiceSummaryResponse? FindService(Guid? serviceId, string? serviceName)
        {
            if (serviceId.HasValue)
            {
                var byId = services.FirstOrDefault(item => item.Id == serviceId.Value);
                if (byId is not null) return byId;
            }

            var key = Normalize(serviceName);
            return key.Length == 0
                ? null
                : services.FirstOrDefault(item => Normalize(item.Name) == key);
        }

        public ServicePrestationSummaryResponse? FindPrestation(
            Guid? prestationId,
            string? prestationName,
            ServiceSummaryResponse? service)
        {
            var candidates = service is null
                ? services.SelectMany(item => item.Prestations)
                : service.Prestations;
            if (prestationId.HasValue)
            {
                var byId = candidates.FirstOrDefault(item => item.Id == prestationId.Value);
                if (byId is not null) return byId;
            }

            var key = Normalize(prestationName);
            return key.Length == 0
                ? null
                : candidates.FirstOrDefault(item => Normalize(item.Name) == key);
        }

        private static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
