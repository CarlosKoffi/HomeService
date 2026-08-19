using HomeService.Provider.Components;
using HomeService.Provider;
using HomeService.Provider.Services;
using HomeService.Contracts.ProviderPortal;

var builder = WebApplication.CreateBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

// Add services to the container.
builder.Services.AddPersistentDataProtection(builder.Configuration, "HomeService.Provider");
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient<ProviderApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["API_BASE_URL"]
        ?? builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080";
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/'));
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

app.MapPost("/activation/submit", async (
    HttpRequest request,
    ProviderApiClient api,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.Redirect("/activation?error=" + Uri.EscapeDataString("Le formulaire d'activation est invalide."));
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var code = form["code"].ToString().Trim();
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();
    var activationUrl = "/activation?code=" + Uri.EscapeDataString(code);

    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.Redirect(activationUrl + "&error=" + Uri.EscapeDataString("Le code d'invitation manque dans ce lien."));
    }

    if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
    {
        return Results.Redirect(activationUrl + "&error=" + Uri.EscapeDataString("Choisissez un mot de passe d'au moins 8 caracteres."));
    }

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
    {
        return Results.Redirect(activationUrl + "&error=" + Uri.EscapeDataString("Les deux mots de passe ne correspondent pas."));
    }

    var result = await api.ActivateAsync(
        new ProviderInvitationActivationRequest(code, password, confirmPassword, false),
        cancellationToken);
    if (!result.IsSuccess || result.Value is null)
    {
        return Results.Redirect(activationUrl + "&error=" + Uri.EscapeDataString(
            result.ErrorMessage ?? "L'activation n'a pas pu etre finalisee."));
    }

    var successUrl = activationUrl
        + "&activated=true"
        + "&phone=" + Uri.EscapeDataString(result.Value.PhoneNumber);
    return Results.Redirect(successUrl);
})
.DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
