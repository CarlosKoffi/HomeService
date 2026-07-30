using HomeService.Api.Auditing;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Clients;
using HomeService.Application.Cms;
using HomeService.Application.Companies;
using HomeService.Application.Contact;
using HomeService.Application.Missions;
using HomeService.Application.Notifications;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Clients;
using HomeService.Contracts.Cms;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Contact;
using HomeService.Contracts.Localization;
using HomeService.Contracts.Missions;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.Services;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "HomeService.Api" }))
            .WithName("HealthCheck");

        app.MapGet("/api/services", async (IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var services = await db.Services
                .AsNoTracking()
                .Include(service => service.Prestations)
                .OrderBy(service => service.Name)
                .Select(service => new ServiceSummaryResponse(
                    service.Id,
                    service.Name,
                    service.Description,
                    service.IconName,
                    service.Status.ToString(),
                    service.IsActive,
                    service.NormalPriceAmount,
                    service.PremiumPriceAmount,
                    service.Currency,
                    service.Prestations
                        .OrderBy(prestation => prestation.SortOrder)
                        .ThenBy(prestation => prestation.Name)
                        .Select(prestation => new ServicePrestationSummaryResponse(
                            prestation.Id,
                            prestation.Name,
                            prestation.Description,
                            prestation.SortOrder,
                            prestation.NormalPriceAmount,
                            prestation.PremiumPriceAmount,
                            prestation.Currency,
                            prestation.IsActive,
                            prestation.PriceMinAmount,
                            prestation.PriceMaxAmount))
                        .ToList(),
                    service.PriceMinAmount,
                    service.PriceMaxAmount,
                    service.IconUrl,
                    service.ImageUrl))
                .ToListAsync(cancellationToken);

            return Results.Ok(services);
        })
        .WithName("ListServices");

        app.MapGet("/api/translations", async (string? scope, string? language, string? country, IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

            var query = db.TranslationValues
                .AsNoTracking()
                .Where(value => value.Language!.Code == languageCode)
                .Where(value => value.Country == null || value.Country.IsoCode == countryCode)
                .Where(value => value.TranslationKey!.IsActive);

            if (!string.IsNullOrWhiteSpace(scope))
            {
                query = query.Where(value => value.TranslationKey!.Scope == scope.Trim());
            }

            var translations = await query
                .OrderBy(value => value.TranslationKey!.Scope)
                .ThenBy(value => value.TranslationKey!.Key)
                .Select(value => new TranslationValueResponse(
                    value.TranslationKey!.Key,
                    value.TranslationKey.Scope,
                    value.Value))
                .ToListAsync(cancellationToken);

            return Results.Ok(translations);
        })
        .WithName("ListTranslations");

        app.MapGet("/api/translations/dictionary", async (string? scope, string? language, string? country, IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var languageCode = string.IsNullOrWhiteSpace(language) ? "fr" : language.Trim().ToLowerInvariant();
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();

            var query = db.TranslationValues
                .AsNoTracking()
                .Where(value => value.Language!.Code == languageCode)
                .Where(value => value.Country == null || value.Country.IsoCode == countryCode)
                .Where(value => value.TranslationKey!.IsActive);

            if (!string.IsNullOrWhiteSpace(scope))
            {
                query = query.Where(value => value.TranslationKey!.Scope == scope.Trim());
            }

            var translations = await query
                .OrderBy(value => value.TranslationKey!.Scope)
                .ThenBy(value => value.TranslationKey!.Key)
                .Select(value => new
                {
                    value.TranslationKey!.Key,
                    value.Value
                })
                .ToDictionaryAsync(value => value.Key, value => value.Value, cancellationToken);

            return Results.Ok(translations);
        })
        .WithName("GetTranslationsDictionary");

        app.MapGet("/api/country-branding", async (string? country, IAppDbContext db, CancellationToken cancellationToken) =>
        {
            var countryCode = string.IsNullOrWhiteSpace(country) ? "CI" : country.Trim().ToUpperInvariant();
            var branding = await db.CountryBrandings
                .AsNoTracking()
                .Where(branding => branding.Country!.IsoCode == countryCode)
                .Select(branding => new CountryBrandingResponse(
                    branding.Country!.IsoCode,
                    branding.Country.Name,
                    branding.BrandName,
                    branding.PrimaryColor,
                    branding.SecondaryColor,
                    branding.AccentColor,
                    branding.HeroTitle,
                    branding.HeroSubtitle,
                    branding.HeroImageUrl,
                    branding.MotifStyle))
                .FirstOrDefaultAsync(cancellationToken);

            return branding is null ? Results.NotFound() : Results.Ok(branding);
        })
        .WithName("GetCountryBranding");

        app.MapGet("/api/cms/company/home", async (
            string? language,
            string? country,
            CompanyHomeCmsQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetAsync(language, country, cancellationToken);
            return response is null
                ? Results.NotFound(new { message = "Contenu CMS entreprise introuvable." })
                : Results.Ok(response);
        })
        .WithName("GetCompanyHomeCmsContent")
        .Produces<CompanyHomeCmsResponse>()
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/cms/provider/home", async (
            string? language,
            string? country,
            ProviderHomeCmsQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var response = await queryService.GetAsync(language, country, cancellationToken);
            return response is null
                ? Results.NotFound(new { message = "Contenu CMS prestataire introuvable." })
                : Results.Ok(response);
        })
        .WithName("GetProviderHomeCmsContent")
        .Produces<CompanyHomeCmsResponse>()
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/cms/media/{id:guid}", async (
            Guid id,
            IAppDbContext db,
            CmsMediaUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var asset = await db.CmsMediaAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(media => media.Id == id, cancellationToken);
            if (asset is null)
            {
                return Results.NotFound(new { message = "Image CMS introuvable." });
            }

            var absolutePath = uploadService.GetAbsolutePath(asset.StoragePath);
            if (!File.Exists(absolutePath))
            {
                return Results.NotFound(new { message = "Le fichier image CMS n'existe plus sur le serveur." });
            }

            return Results.File(absolutePath, asset.ContentType, enableRangeProcessing: true);
        })
        .WithName("GetCmsMedia")
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/contact-requests", async (
            SubmitContactRequest request,
            ContactRequestService contactRequestService,
            CancellationToken cancellationToken) =>
        {
            var result = await contactRequestService.SubmitAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new { message = "Demande contact invalide.", errors = result.Errors });
            }

            return Results.Ok(new SubmitContactResponse(
                result.Id!.Value,
                "Votre message a bien ete transmis. Notre equipe revient vers vous rapidement."));
        })
        .WithName("SubmitContactRequest")
        .Produces<SubmitContactResponse>()
        .Produces(StatusCodes.Status400BadRequest);

        var client = app.MapGroup("/api/client");

        client.MapPost("/auth/register", async (
            RegisterClientRequest request,
            ClientAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.BadRequest(new { message = "Inscription client invalide.", errors = result.Errors });
        })
        .WithName("RegisterClient")
        .Produces<ClientAuthResponse>()
        .Produces(StatusCodes.Status400BadRequest);

        client.MapPost("/auth/login", async (
            LoginClientRequest request,
            ClientAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.BadRequest(new { message = "Connexion client impossible.", errors = result.Errors });
        })
        .WithName("LoginClient")
        .Produces<ClientAuthResponse>()
        .Produces(StatusCodes.Status400BadRequest);

        client.MapPost("/auth/logout", async (
            HttpRequest httpRequest,
            ClientAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var loggedOut = await authService.LogoutAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            return loggedOut ? Results.Ok(new { message = "Session client fermee." }) : Results.Unauthorized();
        })
        .WithName("LogoutClient")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapGet("/me", async (
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            return customer is null ? Results.Unauthorized() : Results.Ok(profileService.ToMe(customer));
        })
        .WithName("GetClientMe")
        .Produces<ClientMeResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapGet("/home", async (
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientHomeService homeService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await homeService.GetAsync(customer, cancellationToken));
        })
        .WithName("GetClientHome")
        .Produces<ClientHomeResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapPut("/me", async (
            UpdateClientProfileRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var response = await profileService.UpdateAsync(customer, request, cancellationToken);
            return Results.Ok(response);
        })
        .WithName("UpdateClientMe")
        .Produces<ClientMeResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapGet("/catalog/search", async (
            string? q,
            ClientCatalogSearchService searchService,
            CancellationToken cancellationToken) =>
        {
            var results = await searchService.SearchAsync(q, cancellationToken);
            return Results.Ok(results);
        })
        .WithName("SearchClientCatalog")
        .Produces<IReadOnlyList<ClientCatalogSearchResultResponse>>();

        client.MapGet("/missions", async (
            string? phoneNumber,
            string? status,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientMissionListService missionListService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            var result = await missionListService.ListAsync(customer?.Id, phoneNumber, status, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Missions)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListClientMissions")
        .Produces<IReadOnlyList<ClientMissionListItemResponse>>()
        .Produces(StatusCodes.Status404NotFound);

        client.MapGet("/missions/{missionId:guid}/messages", async (
            Guid missionId,
            string phoneNumber,
            ClientMissionChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var result = await chatService.ListAsync(missionId, phoneNumber, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.ChatResponse)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListClientMissionMessages")
        .Produces<ClientMissionChatResponse>()
        .Produces(StatusCodes.Status404NotFound);

        client.MapPost("/missions/{missionId:guid}/messages", async (
            Guid missionId,
            SendClientMissionMessageRequest request,
            ClientMissionChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var result = await chatService.SendAsync(missionId, request, cancellationToken);
            if (result.Status == ClientMissionChatResultStatus.Created)
            {
                return Results.Created($"/api/client/missions/{missionId}/messages", result.SendResponse);
            }

            return result.Status switch
            {
                ClientMissionChatResultStatus.NotFound => Results.NotFound(new { message = result.Message }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("SendClientMissionMessage")
        .Produces<SendClientMissionMessageResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        client.MapGet("/addresses", async (
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            return customer is null
                ? Results.Unauthorized()
                : Results.Ok(await profileService.ListAddressesAsync(customer.Id, cancellationToken));
        })
        .WithName("ListClientAddresses")
        .Produces<IReadOnlyList<ClientAddressResponse>>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapPost("/addresses", async (
            UpsertClientAddressRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var response = await profileService.AddAddressAsync(customer.Id, request, cancellationToken);
            return Results.Created($"/api/client/addresses/{response.Id}", response);
        })
        .WithName("CreateClientAddress")
        .Produces<ClientAddressResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapPut("/addresses/{addressId:guid}", async (
            Guid addressId,
            UpsertClientAddressRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var result = await profileService.UpdateAddressAsync(customer.Id, addressId, request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Response) : Results.NotFound();
        })
        .WithName("UpdateClientAddress")
        .Produces<ClientAddressResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        client.MapDelete("/addresses/{addressId:guid}", async (
            Guid addressId,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            return await profileService.DeleteAddressAsync(customer.Id, addressId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .WithName("DeleteClientAddress")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        client.MapGet("/payment-methods", async (
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            return customer is null
                ? Results.Unauthorized()
                : Results.Ok(await profileService.ListPaymentMethodsAsync(customer.Id, cancellationToken));
        })
        .WithName("ListClientPaymentMethods")
        .Produces<IReadOnlyList<ClientPaymentMethodResponse>>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapPost("/payment-methods", async (
            UpsertClientPaymentMethodRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var result = await profileService.AddPaymentMethodAsync(customer.Id, request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/client/payment-methods/{result.Response!.Id}", result.Response)
                : Results.BadRequest(new { message = result.Message });
        })
        .WithName("CreateClientPaymentMethod")
        .Produces<ClientPaymentMethodResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapDelete("/payment-methods/{paymentMethodId:guid}", async (
            Guid paymentMethodId,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientProfileService profileService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            return await profileService.DeletePaymentMethodAsync(customer.Id, paymentMethodId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        })
        .WithName("DeleteClientPaymentMethod")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        client.MapPost("/mobile/device-token", async (
            RegisterMobileDeviceTokenRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            MobileDeviceTokenService deviceTokenService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var result = await deviceTokenService.RegisterAsync(
                MobileDeviceOwnerType.Customer,
                customer.Id,
                request,
                cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.BadRequest(new { message = result.Message });
        })
        .WithName("RegisterClientMobileDeviceToken")
        .Produces<MobileDeviceTokenResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapGet("/notifications", async (
            bool? unreadOnly,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientNotificationInboxService notificationInboxService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await notificationInboxService.ListAsync(customer.Id, unreadOnly == true, cancellationToken));
        })
        .WithName("ListClientNotifications")
        .Produces<ClientNotificationListResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapGet("/notifications/unread-count", async (
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientNotificationInboxService notificationInboxService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await notificationInboxService.CountUnreadAsync(customer.Id, cancellationToken));
        })
        .WithName("GetClientUnreadNotificationCount")
        .Produces<ClientNotificationUnreadCountResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        client.MapPost("/notifications/{notificationId:guid}/mark-read", async (
            Guid notificationId,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientNotificationInboxService notificationInboxService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var result = await notificationInboxService.MarkReadAsync(customer.Id, notificationId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(new { message = result.Message })
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("MarkClientNotificationRead")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/client/mission-photos", async (
            HttpRequest httpRequest,
            ClientMissionPhotoUploadService uploadService,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!httpRequest.HasFormContentType)
                {
                    return Results.BadRequest(new { message = "La photo doit etre envoyee au format multipart/form-data." });
                }

                var form = await httpRequest.ReadFormAsync(cancellationToken);
                var file = form.Files.GetFile("photo") ?? form.Files.FirstOrDefault();
                if (file is null)
                {
                    return Results.BadRequest(new { message = "Ajoutez une photo avant l'envoi." });
                }

                var caption = GetOptionalFormValue(form, "caption");
                var response = await uploadService.SaveAsync(file, caption, cancellationToken);
                return Results.Ok(response);
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Client mission photo upload rejected.");
                return Results.BadRequest(new { message = exception.Message });
            }
        })
        .DisableAntiforgery()
        .WithName("UploadClientMissionPhoto")
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<ClientMissionPhotoUploadResponse>()
        .Produces(StatusCodes.Status400BadRequest);

        app.MapPost("/api/client/missions", async (
            CreateClientMissionRequest request,
            ClientMissionRequestService missionRequestService,
            CancellationToken cancellationToken) =>
        {
            var result = await missionRequestService.CreateAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.BadRequest(new
                {
                    message = "Demande mission invalide.",
                    errors = result.Errors
                });
            }

            return Results.Created($"/api/client/missions/{result.Response!.MissionId}", result.Response);
        })
        .WithName("CreateClientMission")
        .Produces<CreateClientMissionResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/api/client/missions/{missionId:guid}", async (
            Guid missionId,
            string? phoneNumber,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientMissionStatusService missionStatusService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            var resolvedPhoneNumber = customer?.PhoneNumber ?? phoneNumber ?? string.Empty;
            var result = await missionStatusService.GetAsync(missionId, resolvedPhoneNumber, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status switch
            {
                ClientMissionStatusResultStatus.NotFound => Results.NotFound(new { message = result.Message }),
                ClientMissionStatusResultStatus.Forbidden => Results.Problem(
                    title: "Consultation interdite.",
                    detail: result.Message,
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("GetClientMissionStatus")
        .Produces<ClientMissionStatusResponse>()
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/client/missions/{missionId:guid}/screen", async (
            Guid missionId,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientMissionScreenService missionScreenService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is null)
            {
                return Results.Unauthorized();
            }

            var result = await missionScreenService.GetAsync(missionId, customer.PhoneNumber, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status switch
            {
                ClientMissionStatusResultStatus.NotFound => Results.NotFound(new { message = result.Message }),
                ClientMissionStatusResultStatus.Forbidden => Results.Problem(
                    title: "Consultation interdite.",
                    detail: result.Message,
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("GetClientMissionScreen")
        .Produces<ClientMissionScreenResponse>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/client/missions/{missionId:guid}/confirm", async (
            Guid missionId,
            ConfirmClientMissionRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientMissionConfirmationService confirmationService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is not null && string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                request = request with { PhoneNumber = customer.PhoneNumber };
            }

            var result = await confirmationService.ConfirmAsync(missionId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status switch
            {
                ClientMissionConfirmationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                ClientMissionConfirmationStatus.Forbidden => Results.Problem(
                    title: "Confirmation interdite.",
                    detail: result.Message,
                    statusCode: StatusCodes.Status403Forbidden),
                ClientMissionConfirmationStatus.ValidationFailed => Results.BadRequest(new
                {
                    message = result.Message,
                    errors = result.Errors
                }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("ConfirmClientMission")
        .Produces<ConfirmClientMissionResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/client/missions/{missionId:guid}/cancel", async (
            Guid missionId,
            CancelClientMissionRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientMissionCancellationService cancellationService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is not null && string.IsNullOrWhiteSpace(request.CustomerPhoneNumber))
            {
                request = request with { CustomerPhoneNumber = customer.PhoneNumber };
            }

            var result = await cancellationService.CancelAsync(missionId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status switch
            {
                ClientMissionCancellationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                ClientMissionCancellationStatus.Forbidden => Results.Problem(
                    title: "Annulation interdite.",
                    detail: result.Message,
                    statusCode: StatusCodes.Status403Forbidden),
                ClientMissionCancellationStatus.ValidationFailed => Results.BadRequest(new
                {
                    message = result.Message,
                    errors = result.Errors
                }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("CancelClientMission")
        .Produces<CancelClientMissionResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/client/missions/{missionId:guid}/validate-completion", async (
            Guid missionId,
            ValidateClientMissionCompletionRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            ClientMissionCompletionValidationService completionValidationService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is not null && string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                request = request with { PhoneNumber = customer.PhoneNumber };
            }

            var result = await completionValidationService.ValidateAsync(missionId, request, cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.Status switch
            {
                ClientMissionCompletionValidationStatus.NotFound => Results.NotFound(new { message = result.Message }),
                ClientMissionCompletionValidationStatus.Forbidden => Results.Problem(
                    title: "Validation interdite.",
                    detail: result.Message,
                    statusCode: StatusCodes.Status403Forbidden),
                ClientMissionCompletionValidationStatus.ValidationFailed => Results.BadRequest(new
                {
                    message = result.Message,
                    errors = result.Errors
                }),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("ValidateClientMissionCompletion")
        .Produces<ValidateClientMissionCompletionResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/client/missions/{missionId:guid}/additional-quotes/{quoteId:guid}/pay", async (
            Guid missionId,
            Guid quoteId,
            PayMissionAdditionalQuoteRequest request,
            HttpRequest httpRequest,
            ClientAuthService authService,
            MissionAdditionalQuoteWorkflowService additionalQuoteService,
            CancellationToken cancellationToken) =>
        {
            var customer = await authService.GetSessionCustomerAsync(httpRequest.Headers.Authorization.ToString(), cancellationToken);
            if (customer is not null && string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                request = request with { PhoneNumber = customer.PhoneNumber };
            }

            var result = await additionalQuoteService.PayByCustomerAsync(quoteId, request, cancellationToken);
            if (result.IsSuccess && result.Response?.MissionId == missionId)
            {
                return Results.Ok(result.Response);
            }

            if (result.IsSuccess)
            {
                return Results.NotFound(new { message = "Devis complementaire introuvable pour cette mission." });
            }

            return result.Status switch
            {
                MissionAdditionalQuoteWorkflowStatus.NotFound => Results.NotFound(new { message = result.Message }),
                MissionAdditionalQuoteWorkflowStatus.Forbidden => Results.Problem(
                    title: "Paiement complementaire interdit.",
                    detail: result.Message,
                    statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new { message = result.Message })
            };
        })
        .WithName("PayClientMissionAdditionalQuote")
        .Produces<MissionAdditionalQuoteResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        app.MapPost("/api/company-applications", async (
            HttpRequest httpRequest,
            CompanyApplicationUploadService uploadService,
            CompanyApplicationRegistrationService registrationService,
            IAppDbContext db,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            try
            {
                if (!httpRequest.HasFormContentType)
                {
                    return Results.BadRequest(new { message = "Le formulaire doit etre envoye au format multipart/form-data." });
                }

                logger.LogInformation("Company application submission received.");
                var form = await httpRequest.ReadFormAsync(cancellationToken);
                var request = new RegisterCompanyRequest(
                    GetFormValue(form, "companyName"),
                    GetOptionalFormValue(form, "registrationNumber"),
                    GetFormValue(form, "city"),
                    GetOptionalFormValue(form, "address"),
                    GetFormValue(form, "contactName"),
                    GetFormValue(form, "email"),
                    GetFormValue(form, "phoneNumber"),
                    GetFormValue(form, "password"),
                    GetFormValue(form, "confirmPassword"),
                    GetServices(form),
                    GetOptionalInt(form, "estimatedProviderCount"));

                var applicationId = Guid.NewGuid();
                var documents = await uploadService.SaveAsync(applicationId, form.Files, cancellationToken);
                var result = await registrationService.RegisterAsync(
                    request,
                    applicationId,
                    documents.Select(document => new CompanyApplicationUploadedDocument(
                            document.DocumentType,
                            document.OriginalFileName,
                            document.StoragePath,
                            document.ContentType))
                        .ToList(),
                    cancellationToken);

                if (result.Status == CompanyApplicationRegistrationStatus.ValidationFailed)
                {
                    return Results.BadRequest(new { message = result.Message, errors = result.Errors });
                }

                if (result.Status == CompanyApplicationRegistrationStatus.DuplicateEmail)
                {
                    return Results.BadRequest(new { message = result.Message });
                }

                var application = result.Application!;
                var company = result.Company!;
                logger.LogInformation("Stored {DocumentCount} company application documents for {ApplicationId}.", result.DocumentCount, application.Id);
                db.AuditLogEntries.Add(AuditLogFactory.Create(
                    AuditActor.Company(company.Id, company.Name),
                    "CompanyApplicationSubmitted",
                    nameof(HomeService.Domain.Entities.CompanyApplication),
                    application.Id,
                    "Demande entreprise creee depuis le formulaire public.",
                    HttpAuditContextFactory.Create(httpRequest),
                    after: new
                    {
                        application.CompanyName,
                        application.Email,
                        application.City,
                        result.ServiceCount,
                        result.DocumentCount,
                        application.Status
                    }));
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Company application {ApplicationId} saved.", application.Id);

                return Results.Created($"/api/admin/company-applications/{application.Id}", new { application.Id });
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Company application submission rejected.");
                return Results.BadRequest(new { message = exception.Message });
            }
            catch (OperationCanceledException exception)
            {
                logger.LogWarning(exception, "Company application submission was cancelled while reading the form.");
                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (BadHttpRequestException exception)
            {
                logger.LogWarning(exception, "Company application submission was interrupted while reading uploaded files.");
                return Results.BadRequest(new { message = "L'envoi des pieces a ete interrompu. Verifiez la connexion puis relancez l'envoi." });
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Company application submission failed.");
                return Results.Problem(
                    title: "Impossible d'enregistrer la demande entreprise",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("RegisterCompanyApplication");

        return app;
    }

    private static string GetFormValue(IFormCollection form, string key)
    {
        return form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }

    private static string? GetOptionalFormValue(IFormCollection form, string key)
    {
        var value = GetFormValue(form, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? GetOptionalInt(IFormCollection form, string key)
    {
        return int.TryParse(GetFormValue(form, key), out var value) ? value : null;
    }

    private static IReadOnlyList<string> GetServices(IFormCollection form)
    {
        if (!form.TryGetValue("services", out var values))
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

}
