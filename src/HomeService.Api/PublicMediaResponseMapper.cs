using HomeService.Contracts.Clients;
using HomeService.Contracts.Services;

namespace HomeService.Api;

public static class PublicMediaResponseMapper
{
    public static string? Resolve(IApiObjectStorage storage, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var objectKey = value.Trim();
        if (Uri.TryCreate(objectKey, UriKind.Absolute, out var absoluteUri)
            && (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(absoluteUri.Host, "media.wele.africa", StringComparison.OrdinalIgnoreCase))
            {
                return storage.GetPublicUrl(absoluteUri.AbsolutePath.TrimStart('/')) ?? value;
            }

            return value;
        }

        return storage.GetPublicUrl(objectKey.TrimStart('/')) ?? value;
    }

    public static ServiceSummaryResponse Map(IApiObjectStorage storage, ServiceSummaryResponse service)
    {
        return service with
        {
            IconUrl = Resolve(storage, service.IconUrl),
            ImageUrl = Resolve(storage, service.ImageUrl),
            Prestations = service.Prestations
                .Select(prestation => prestation with
                {
                    IllustrationUrl = Resolve(storage, prestation.IllustrationUrl)
                })
                .ToList()
        };
    }

    public static ClientCatalogSearchResultResponse Map(
        IApiObjectStorage storage,
        ClientCatalogSearchResultResponse result)
    {
        return result with
        {
            IconUrl = Resolve(storage, result.IconUrl),
            ImageUrl = Resolve(storage, result.ImageUrl)
        };
    }

    public static PrepareClientMissionResponse Map(
        IApiObjectStorage storage,
        PrepareClientMissionResponse response)
    {
        return response with
        {
            IconUrl = Resolve(storage, response.IconUrl),
            ImageUrl = Resolve(storage, response.ImageUrl)
        };
    }

    public static ClientMissionListItemResponse Map(
        IApiObjectStorage storage,
        ClientMissionListItemResponse mission)
    {
        return mission with { IconUrl = Resolve(storage, mission.IconUrl) };
    }

    public static ClientHomeResponse Map(IApiObjectStorage storage, ClientHomeResponse home)
    {
        return home with
        {
            HighlightMission = home.HighlightMission is null ? null : Map(storage, home.HighlightMission),
            RecentMissions = home.RecentMissions.Select(mission => Map(storage, mission)).ToList()
        };
    }

    public static PaymentProviderResponse Map(IApiObjectStorage storage, PaymentProviderResponse provider)
    {
        return provider with { LogoUrl = Resolve(storage, provider.LogoUrl) };
    }

    public static ClientPaymentMethodResponse Map(IApiObjectStorage storage, ClientPaymentMethodResponse method)
    {
        return method with
        {
            PaymentProviderLogoUrl = Resolve(storage, method.PaymentProviderLogoUrl)
        };
    }

    public static CreateClientMobileMoneyAccountResponse Map(
        IApiObjectStorage storage,
        CreateClientMobileMoneyAccountResponse response)
    {
        return response with
        {
            PaymentMethods = response.PaymentMethods.Select(method => Map(storage, method)).ToList()
        };
    }
}
