using HomeService.Client.Components;
using HomeService.Client;
using HomeService.Client.Services;

var builder = WebApplication.CreateBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddPersistentDataProtection(builder.Configuration, "HomeService.Client");
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<PublicWebsiteApiClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["API_BASE_URL"]
        ?? builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080");
    client.Timeout = TimeSpan.FromSeconds(8);
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
app.MapPublicSeoEndpoints();
app.MapGet("/telecharger/android", (IConfiguration configuration) =>
    Results.Redirect(configuration["ClientApp:AndroidUrl"]
        ?? "https://play.google.com/store/apps/details?id=ci.wele.client"));
app.MapGet("/telecharger/ios", (IConfiguration configuration) =>
{
    var target = configuration["ClientApp:IosUrl"];
    return Results.Redirect(string.IsNullOrWhiteSpace(target) || target.StartsWith('#')
        ? "https://wele.africa/#telecharger"
        : target);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
