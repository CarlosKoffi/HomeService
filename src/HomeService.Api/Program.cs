using HomeService.Application.Abstractions;
using HomeService.Contracts.Companies;
using HomeService.Contracts.Services;
using HomeService.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "HomeService API",
        Version = "v1",
        Description = "API centrale pour la plateforme HomeService: services, entreprises, validation admin et futurs parcours client/prestataire."
    });
});
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "HomeService API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeService API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "HomeService.Api" }))
    .WithName("HealthCheck");

app.MapGet("/api/services", async (IAppDbContext db, CancellationToken cancellationToken) =>
{
    var services = await db.Services
        .AsNoTracking()
        .OrderBy(service => service.Name)
        .Select(service => new ServiceSummaryResponse(
            service.Id,
            service.Name,
            service.Description,
            service.Status.ToString(),
            service.IsActive))
        .ToListAsync(cancellationToken);

    return Results.Ok(services);
})
.WithName("ListServices");

var admin = app.MapGroup("/api/admin");

admin.MapGet("/company-applications", async (IAppDbContext db, CancellationToken cancellationToken) =>
{
    var applications = await db.CompanyApplications
        .AsNoTracking()
        .Select(application => new CompanyApplicationSummaryResponse(
            application.Id,
            application.CompanyName,
            application.City,
            application.ContactName,
            application.Email,
            application.PhoneNumber,
            application.Status.ToString(),
            application.SubmittedAt,
            application.LastReminderSentAt,
            application.ActivationEmailSentAt,
            application.Documents.Count,
            application.Documents.Count(document => document.ReviewStatus == HomeService.Domain.Enums.DocumentReviewStatus.Pending)))
        .OrderBy(application => application.Status == "Approved" || application.Status == "ActivationSent" || application.Status == "Activated")
        .ThenByDescending(application => application.SubmittedAt)
        .ToListAsync(cancellationToken);

    return Results.Ok(applications);
})
.WithName("ListCompanyApplications");

admin.MapGet("/company-applications/{id:guid}", async (Guid id, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .AsNoTracking()
        .Where(application => application.Id == id)
        .Select(application => new CompanyApplicationDetailResponse(
            application.Id,
            application.CompanyName,
            application.RegistrationNumber,
            application.City,
            application.Address,
            application.ContactName,
            application.Email,
            application.PhoneNumber,
            application.PlannedServices,
            application.EstimatedProviderCount,
            application.Status.ToString(),
            application.SubmittedAt,
            application.ReviewedAt,
            application.LastReminderSentAt,
            application.ActivationEmailSentAt,
            application.ReviewNote,
            application.Documents
                .OrderBy(document => document.DocumentType)
                .Select(document => new CompanyApplicationDocumentResponse(
                    document.Id,
                    document.DocumentType.ToString(),
                    document.OriginalFileName,
                    document.ContentType,
                    document.ReviewStatus.ToString(),
                    document.ReviewNote,
                    document.CreatedAt))
                .ToList()))
        .FirstOrDefaultAsync(cancellationToken);

    return application is null ? Results.NotFound() : Results.Ok(application);
})
.WithName("GetCompanyApplication");

app.Run();

public partial class Program;
