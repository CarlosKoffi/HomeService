using HomeService.Company.Components;
using HomeService.Company;
using HomeService.Company.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddPersistentDataProtection(builder.Configuration, "HomeService.Company");
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<PlatformApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["API_BASE_URL"] ?? builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080");
    client.Timeout = TimeSpan.FromMinutes(10);
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

app.MapStaticAssets();
app.MapGet("/provider-documents/{documentId:guid}/preview", async (
    Guid documentId,
    PlatformApiClient apiClient,
    CancellationToken cancellationToken) =>
{
    var document = await apiClient.GetProviderDocumentPreviewAsync(documentId, cancellationToken);
    return document is null
        ? Results.NotFound(new { message = "Le fichier n'existe plus sur le serveur." })
        : Results.File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
