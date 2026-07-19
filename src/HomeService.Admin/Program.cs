using HomeService.Admin.Components;
using HomeService.Admin;
using HomeService.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddPersistentDataProtection(builder.Configuration, "HomeService.Admin");
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<PlatformApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["API_BASE_URL"] ?? builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (string.Equals(app.Configuration["FORCE_HTTPS_REDIRECT"], "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseSiteAccessGate();

app.UseAntiforgery();

app.MapGet("/admin-documents/{documentId:guid}/preview", async (
    Guid documentId,
    PlatformApiClient apiClient,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var document = await apiClient.GetCompanyApplicationDocumentFileAsync(documentId, cancellationToken);
    context.Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName.Replace("\"", string.Empty)}\"";

    return Results.File(document.Content, document.ContentType, enableRangeProcessing: true);
});

app.MapGet("/admin-provider-documents/{documentId:guid}/preview", async (
    Guid documentId,
    PlatformApiClient apiClient,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var document = await apiClient.GetProviderDocumentFileAsync(documentId, cancellationToken);
    context.Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName.Replace("\"", string.Empty)}\"";

    return Results.File(document.Content, document.ContentType, enableRangeProcessing: true);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
