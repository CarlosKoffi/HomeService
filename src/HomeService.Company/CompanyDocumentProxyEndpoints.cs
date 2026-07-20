using HomeService.Company.Services;

namespace HomeService.Company;

public static class CompanyDocumentProxyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyDocumentProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/provider-documents/{documentId:guid}/preview", async (
            Guid documentId,
            PlatformApiClient apiClient,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var document = await apiClient.GetProviderDocumentPreviewAsync(documentId, cancellationToken);
                return document is null
                    ? Results.NotFound(new { message = "Le fichier n'existe plus sur le serveur." })
                    : Results.File(
                        document.Content,
                        document.ContentType,
                        document.FileName,
                        enableRangeProcessing: true);
            }
            catch (HttpRequestException exception)
            {
                return Results.Problem(
                    title: "Document indisponible.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        });

        return app;
    }
}
