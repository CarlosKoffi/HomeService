using HomeService.Admin.Services;

namespace HomeService.Admin;

public static class AdminDocumentProxyEndpoints
{
    public static IEndpointRouteBuilder MapAdminDocumentProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin-documents/{documentId:guid}/preview", async (
            Guid documentId,
            PlatformApiClient apiClient,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            return await RenderDocumentAsync(
                () => apiClient.GetCompanyApplicationDocumentFileAsync(documentId, cancellationToken),
                context);
        });

        app.MapGet("/admin-provider-documents/{documentId:guid}/preview", async (
            Guid documentId,
            PlatformApiClient apiClient,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            return await RenderDocumentAsync(
                () => apiClient.GetProviderDocumentFileAsync(documentId, cancellationToken),
                context);
        });

        app.MapGet("/admin-cms-media/{mediaId:guid}/preview", async (
            Guid mediaId,
            PlatformApiClient apiClient,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            return await RenderDocumentAsync(
                () => apiClient.GetCmsMediaFileAsync(mediaId, cancellationToken),
                context);
        });

        return app;
    }

    private static async Task<IResult> RenderDocumentAsync(
        Func<Task<CompanyApplicationDocumentFile>> getDocument,
        HttpContext context)
    {
        try
        {
            var document = await getDocument();
            context.Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizeFileName(document.FileName)}\"";

            return Results.File(document.Content, document.ContentType, enableRangeProcessing: true);
        }
        catch (PlatformApiException exception)
        {
            return Results.Problem(
                title: "Document indisponible.",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        return fileName.Replace("\"", string.Empty, StringComparison.Ordinal);
    }
}
