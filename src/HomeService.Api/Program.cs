using HomeService.Application.Abstractions;
using HomeService.Api;
using HomeService.Api.Auditing;
using HomeService.Api.Endpoints;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Contracts.CompanyPortal;
using HomeService.Contracts.Notifications;
using HomeService.Contracts.ProviderPortal;
using HomeService.Contracts.Monitoring;
using HomeService.Application.ProviderPortal;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Application.Admin;
using HomeService.Application.CompanyPortal;
using HomeService.Application.Security;
using HomeService.Application.Notifications;
using HomeService.Application.Auditing;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using HomeService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
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
builder.Services.AddSingleton<CompanyApplicationUploadService>();
builder.Services.AddSingleton<CompanyProviderUploadService>();
builder.Services.AddScoped<ProviderMissionWorkflowService>();
builder.Services.AddScoped<CompanyApplicationRegistrationService>();
builder.Services.AddScoped<CompanyPortalAuthService>();
builder.Services.AddScoped<CompanyActivationPreviewService>();
builder.Services.AddScoped<CompanyActivationLinkGenerationService>();
builder.Services.AddScoped<CompanyActivationPasswordService>();
builder.Services.AddScoped<CompanyComplianceDocumentService>();
builder.Services.AddScoped<CompanyEmployeeInvitationService>();
builder.Services.AddScoped<CompanyEmployeeManagementService>();
builder.Services.AddScoped<CompanyInterimCandidateService>();
builder.Services.AddScoped<CompanyPortalQueryService>();
builder.Services.AddScoped<ProviderSelfRegistrationService>();
builder.Services.AddScoped<ProviderOnboardingService>();
builder.Services.AddScoped<ProviderPortalAuthService>();
builder.Services.AddScoped<AdminCompanyApplicationReviewService>();
builder.Services.AddScoped<AdminCompanyApplicationDocumentReviewService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSiteAccessGate();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "HomeService API";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HomeService API v1");
    options.RoutePrefix = "swagger";
});

if (string.Equals(app.Configuration["FORCE_HTTPS_REDIRECT"], "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.MapPublicEndpoints();
app.MapProviderOnboardingEndpoints();
app.MapCompanyActivationEndpoints();
app.MapCompanyPortalEndpoints();

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
        AddAuditLog(
            db,
            httpRequest,
            AuditActor.Company(company.Id, company.Name),
            "CompanyApplicationSubmitted",
            nameof(HomeService.Domain.Entities.CompanyApplication),
            application.Id,
            "Demande entreprise creee depuis le formulaire public.",
            after: new
            {
                application.CompanyName,
                application.Email,
                application.City,
                result.ServiceCount,
                result.DocumentCount,
                application.Status
            });
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
    catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception)
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

app.MapPost("/api/company-portal/{companyId:guid}/employees", async (
    Guid companyId,
    HttpRequest httpRequest,
    IAppDbContext db,
    CompanyProviderUploadService uploadService,
    CompanyEmployeeInvitationService invitationService,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Le formulaire employe doit etre envoye au format multipart/form-data." });
    }

    var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == companyId && company.Status != CompanyStatus.Suspended, cancellationToken);
    if (company is null)
    {
        return Results.NotFound(new { message = "Entreprise introuvable ou inactive." });
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);
    var errors = CompanyProviderValidator.Validate(ToCompanyProviderFormData(form));
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { message = "Le formulaire employe contient des erreurs.", errors });
    }

    var provider = new ProviderProfile(
        companyId,
        GetFormValue(form, "firstName"),
        GetFormValue(form, "lastName"),
        GetFormValue(form, "phoneNumber"),
        DateOnly.Parse(GetFormValue(form, "dateOfBirth")),
        GetFormValue(form, "address"),
        ParseProviderGender(GetOptionalFormValue(form, "gender")),
        ParseProviderEmploymentType(GetOptionalFormValue(form, "employmentType")),
        GetOptionalInt(form, "yearsOfExperience") ?? 0,
        GetOptionalDecimal(form, "missionLatitude"),
        GetOptionalDecimal(form, "missionLongitude"),
        GetOptionalInt(form, "missionRadiusKm") ?? 5);

    var requestedServiceIds = GetGuidValues(form, "serviceIds");
    var services = await db.Services
        .Where(service => requestedServiceIds.Contains(service.Id) && service.IsActive)
        .Select(service => service.Id)
        .ToListAsync(cancellationToken);

    foreach (var serviceId in services)
    {
        provider.AddService(
            serviceId,
            ParseExperienceLevel(GetOptionalFormValue(form, "experienceLevel")));
    }

    db.Providers.Add(provider);

    var invitation = await invitationService.CreatePendingInvitationAsync(
        provider.Id,
        companyId,
        configuration["PROVIDER_PORTAL_BASE_URL"],
        cancellationToken);

    var documents = await uploadService.SaveAsync(companyId, provider.Id, form.Files, cancellationToken);
    foreach (var document in documents)
    {
        provider.AttachDocument(new ProviderDocument(
            provider.Id,
            document.DocumentType,
            document.OriginalFileName,
            document.StoragePath,
            document.ContentType));
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Company(company.Id, company.Name),
        "CompanyEmployeeCreated",
        nameof(ProviderProfile),
        provider.Id,
        "Prestataire ajoute depuis le portail entreprise.",
        after: new
        {
            provider.FirstName,
            provider.LastName,
            provider.PhoneNumber,
            provider.EmploymentType,
            provider.Gender,
            provider.YearsOfExperience,
            ServiceCount = services.Count,
            DocumentCount = documents.Count
        });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created(
        $"/api/company-portal/{companyId}/employees/{provider.Id}",
        new CreateCompanyEmployeeResult(provider.Id, invitation.Code, invitation.InvitationLink, invitation.ExpiresAt));
})
.WithName("CreateCompanyPortalEmployee");

app.MapPost("/api/company-portal/{companyId:guid}/employees/{employeeId:guid}/invitation-code", async (
    Guid companyId,
    Guid employeeId,
    HttpRequest httpRequest,
    IAppDbContext db,
    CompanyEmployeeInvitationService invitationService,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var result = await invitationService.RegenerateAsync(
        companyId,
        employeeId,
        configuration["PROVIDER_PORTAL_BASE_URL"],
        cancellationToken);
    if (result.Status == CompanyEmployeeInvitationStatus.NotFound)
    {
        return Results.NotFound(new { message = "Prestataire introuvable." });
    }

    var invitation = result.Invitation!;

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Company(companyId, null),
        "ProviderInvitationCodeGenerated",
        nameof(ProviderInvitation),
        invitation.Id,
        "Code d'acces prestataire genere depuis le portail entreprise.",
        after: new { ProviderId = result.ProviderId, invitation.Code, invitation.ExpiresAt });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new CreateCompanyEmployeeResult(result.ProviderId!.Value, invitation.Code, invitation.InvitationLink, invitation.ExpiresAt));
})
.WithName("GenerateCompanyPortalEmployeeInvitationCode");

var providerPortal = app.MapGroup("/api/provider-portal");

providerPortal.MapGet("/invitations/{code}", async (
    string code,
    ProviderPortalAuthService authService,
    CancellationToken cancellationToken) =>
{
    var invitation = await authService.GetInvitationAsync(code, cancellationToken);
    return invitation is null
        ? Results.NotFound(new { message = "Code de preinscription introuvable." })
        : Results.Ok(invitation);
})
.WithName("GetProviderInvitation");

providerPortal.MapPost("/activate", async (
    ProviderInvitationActivationRequest request,
    HttpRequest httpRequest,
    ProviderPortalAuthService authService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await authService.ActivateInvitationAsync(request, cancellationToken);
    if (!result.IsSuccess || result.Response is null || result.Provider is null)
    {
        return Results.BadRequest(new { message = result.ErrorMessage ?? "Activation impossible." });
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Provider(result.Provider.Id, result.Provider.FullName),
        "ProviderPortalActivated",
        nameof(ProviderProfile),
        result.Provider.Id,
        "Compte prestataire active depuis un code entreprise.",
        after: new { result.Provider.Status, result.Provider.CompanyId });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(result.Response);
})
.WithName("ActivateProviderInvitation");

providerPortal.MapPost("/login", async (
    ProviderPortalLoginRequest request,
    HttpRequest httpRequest,
    ProviderPortalAuthService authService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await authService.LoginAsync(request, cancellationToken);
    if (!result.IsSuccess || result.Response is null || result.Provider is null)
    {
        return Results.BadRequest(new { message = result.ErrorMessage ?? "Connexion impossible." });
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Provider(result.Provider.Id, result.Provider.FullName),
        "ProviderPortalLogin",
        nameof(ProviderPortalSession),
        result.Session?.Id,
        "Connexion prestataire.",
        after: new { result.Provider.Status, result.Response.ExpiresAt });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(result.Response);
})
.WithName("LoginProviderPortal");

providerPortal.MapGet("/me", async (
    HttpRequest httpRequest,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
    if (session?.Provider is null)
    {
        return Results.Unauthorized();
    }

    var provider = session.Provider;
    return Results.Ok(new ProviderPortalMeResponse(
        provider.Id,
        provider.FullName,
        provider.PhoneNumber,
        provider.Company?.Name,
        provider.Status.ToString(),
        provider.Status == ProviderStatus.Approved && provider.CompanyId is not null,
        provider.IsAvailable));
})
.WithName("GetProviderPortalMe");

providerPortal.MapGet("/mobile/home", async (
    HttpRequest httpRequest,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
    if (session?.Provider is null)
    {
        return Results.Unauthorized();
    }

    var provider = await db.Providers
        .AsNoTracking()
        .Include(provider => provider.Company)
        .Include(provider => provider.Documents)
        .Include(provider => provider.Services)
            .ThenInclude(providerService => providerService.Service)
        .FirstOrDefaultAsync(provider => provider.Id == session.ProviderId, cancellationToken);

    if (provider is null)
    {
        return Results.Unauthorized();
    }

    var now = DateTimeOffset.UtcNow;
    var assignments = await db.ProviderMissionAssignments
        .AsNoTracking()
        .Include(assignment => assignment.Company)
        .Include(assignment => assignment.Mission)
        .Where(assignment =>
            assignment.ProviderId == provider.Id
            && assignment.Status != ProviderMissionAssignmentStatus.Refused
            && assignment.Status != ProviderMissionAssignmentStatus.Completed
            && assignment.Status != ProviderMissionAssignmentStatus.Expired)
        .OrderBy(assignment => assignment.Mission!.ScheduledFor ?? assignment.ExpiresAt)
        .Take(6)
        .ToListAsync(cancellationToken);

    var missionRows = assignments
        .Where(assignment => assignment.Mission is not null)
        .Select(assignment => assignment.Mission!)
        .ToList();
    var serviceIds = missionRows.Select(mission => mission.ServiceId).Distinct().ToList();
    var customerIds = missionRows.Select(mission => mission.CustomerId).Distinct().ToList();
    var servicesById = await db.Services
        .AsNoTracking()
        .Where(service => serviceIds.Contains(service.Id))
        .ToDictionaryAsync(service => service.Id, cancellationToken);
    var customersById = await db.Customers
        .AsNoTracking()
        .Where(customer => customerIds.Contains(customer.Id))
        .ToDictionaryAsync(customer => customer.Id, cancellationToken);

    var liveOffer = assignments
        .Where(assignment => assignment.Status == ProviderMissionAssignmentStatus.Offered && assignment.ExpiresAt > now)
        .OrderBy(assignment => assignment.ExpiresAt)
        .Select(assignment => ToProviderMobileMissionOffer(assignment, provider, now, servicesById, customersById))
        .FirstOrDefault();

    var upcomingMission = assignments
        .Where(assignment => assignment.Status != ProviderMissionAssignmentStatus.Offered || assignment.ExpiresAt <= now)
        .OrderBy(assignment => assignment.Mission!.ScheduledFor ?? assignment.ExpiresAt)
        .Select(assignment => ToProviderMobileMissionSummary(assignment, servicesById, customersById))
        .FirstOrDefault();

    return Results.Ok(new ProviderMobileHomeResponse(
        new ProviderMobileStatusResponse(
            provider.FullName,
            provider.Company?.Name ?? "En attente d'entreprise",
            provider.IsAvailable,
            provider.IsAvailable ? "Disponible" : "Indisponible",
            provider.MissionRadiusKm),
        BuildProviderMobileProfileCompletion(provider),
        upcomingMission,
        liveOffer));
})
.WithName("GetProviderMobileHome");

providerPortal.MapPost("/mission-assignments/{assignmentId:guid}/accept", async (
    Guid assignmentId,
    ProviderAcceptMissionRequest request,
    HttpRequest httpRequest,
    IAppDbContext db,
    ProviderMissionWorkflowService workflow,
    CancellationToken cancellationToken) =>
{
    var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
    if (session?.Provider is null)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.ProviderMissionAssignments
        .Include(assignment => assignment.Mission)
        .FirstOrDefaultAsync(assignment =>
            assignment.Id == assignmentId
            && assignment.ProviderId == session.ProviderId,
            cancellationToken);

    if (assignment?.Mission is null)
    {
        return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
    }

    var result = workflow.AcceptMission(session.Provider, assignment, request);
    if (result.Status != ProviderMissionOperationStatus.Ok)
    {
        return ToProviderMissionHttpResult(result);
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Provider(session.ProviderId, $"{session.Provider.FirstName} {session.Provider.LastName}"),
        "ProviderMissionAccepted",
        nameof(ProviderMissionAssignment),
        assignment.Id,
        "Mission acceptee par le prestataire. Les contacts restent masques jusqu'a validation client.",
        after: new
        {
            assignment.MissionId,
            AssignmentStatus = assignment.Status,
            MissionStatus = assignment.Mission.Status,
            assignment.AcceptedLatitude,
            assignment.AcceptedLongitude,
            assignment.AcceptedAccuracyMeters,
            assignment.Mission.ProviderAcceptedAt,
            assignment.Mission.ContactDetailsReleasedAt
        });
    await db.SaveChangesAsync(cancellationToken);
    return ToProviderMissionHttpResult(result);
})
.WithName("AcceptProviderMission");

providerPortal.MapPost("/mission-assignments/{assignmentId:guid}/verify-arrival", async (
    Guid assignmentId,
    ProviderLocationVerificationRequest request,
    HttpRequest httpRequest,
    IAppDbContext db,
    ProviderMissionWorkflowService workflow,
    CancellationToken cancellationToken) =>
{
    var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
    if (session?.Provider is null)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.ProviderMissionAssignments
        .Include(assignment => assignment.Mission)
        .FirstOrDefaultAsync(assignment =>
            assignment.Id == assignmentId
            && assignment.ProviderId == session.ProviderId,
            cancellationToken);

    if (assignment?.Mission is null)
    {
        return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
    }

    var result = workflow.VerifyArrival(session.Provider, assignment, request);
    if (result.Status != ProviderMissionOperationStatus.Ok)
    {
        return ToProviderMissionHttpResult(result);
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Provider(session.ProviderId, $"{session.Provider.FirstName} {session.Provider.LastName}"),
        "ProviderArrivalVerified",
        nameof(ProviderMissionAssignment),
        assignment.Id,
        "Arrivee prestataire verifiee pour une mission.",
        after: new
        {
            assignment.MissionId,
            assignment.ArrivalVerificationStatus,
            assignment.ArrivalVerifiedAt,
            assignment.ArrivalDistanceMeters
        });
    await db.SaveChangesAsync(cancellationToken);
    return ToProviderMissionHttpResult(result);
})
.WithName("VerifyProviderMissionArrival");

providerPortal.MapPost("/mission-assignments/{assignmentId:guid}/start", async (
    Guid assignmentId,
    ProviderLocationVerificationRequest request,
    HttpRequest httpRequest,
    IAppDbContext db,
    ProviderMissionWorkflowService workflow,
    CancellationToken cancellationToken) =>
{
    var session = await GetProviderPortalSessionAsync(httpRequest, db, cancellationToken);
    if (session?.Provider is null)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.ProviderMissionAssignments
        .Include(assignment => assignment.Mission)
        .FirstOrDefaultAsync(assignment =>
            assignment.Id == assignmentId
            && assignment.ProviderId == session.ProviderId,
            cancellationToken);

    if (assignment?.Mission is null)
    {
        return Results.NotFound(new { message = "Mission introuvable pour ce prestataire." });
    }

    var result = workflow.StartMission(session.Provider, assignment, request);
    if (result.Status != ProviderMissionOperationStatus.Ok)
    {
        if (result.Response is not null)
        {
            AddAuditLog(
                db,
                httpRequest,
                AuditActor.Provider(session.ProviderId, $"{session.Provider.FirstName} {session.Provider.LastName}"),
                "ProviderMissionStartRejected",
                nameof(ProviderMissionAssignment),
                assignment.Id,
                result.Message ?? "Demarrage mission refuse.",
                after: new
                {
                    assignment.MissionId,
                    result.Response.Status,
                    result.Response.DistanceMeters
                });
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToProviderMissionHttpResult(result);
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Provider(session.ProviderId, $"{session.Provider.FirstName} {session.Provider.LastName}"),
        "ProviderMissionStarted",
        nameof(ProviderMissionAssignment),
        assignment.Id,
        "Mission demarree par le prestataire.",
        after: new
        {
            assignment.MissionId,
            AssignmentStatus = assignment.Status,
            MissionStatus = assignment.Mission.Status,
            assignment.StartedAt
        });
    await db.SaveChangesAsync(cancellationToken);
    return ToProviderMissionHttpResult(result);
})
.WithName("StartProviderMissionWithArrivalVerification");

var admin = app.MapGroup("/api/admin");

admin.MapGet("/audit-logs", async (
    string? actorType,
    Guid? actorId,
    string? action,
    string? entityType,
    Guid? entityId,
    string? search,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int? skip,
    int? take,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var pageSize = Math.Clamp(take ?? 50, 1, 200);
    var offset = Math.Max(skip ?? 0, 0);
    var query = db.AuditLogEntries.AsNoTracking();

    if (Enum.TryParse<AuditActorType>(actorType, true, out var parsedActorType))
    {
        query = query.Where(entry => entry.ActorType == parsedActorType);
    }

    if (actorId.HasValue)
    {
        query = query.Where(entry => entry.ActorId == actorId.Value);
    }

    if (!string.IsNullOrWhiteSpace(action))
    {
        var normalizedAction = action.Trim();
        query = query.Where(entry => entry.Action == normalizedAction);
    }

    if (!string.IsNullOrWhiteSpace(entityType))
    {
        var normalizedEntityType = entityType.Trim();
        query = query.Where(entry => entry.EntityType == normalizedEntityType);
    }

    if (entityId.HasValue)
    {
        query = query.Where(entry => entry.EntityId == entityId.Value);
    }

    if (from.HasValue)
    {
        query = query.Where(entry => entry.OccurredAt >= from.Value);
    }

    if (to.HasValue)
    {
        query = query.Where(entry => entry.OccurredAt <= to.Value);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLowerInvariant();
        query = query.Where(entry =>
            entry.Action.ToLower().Contains(term)
            || entry.EntityType.ToLower().Contains(term)
            || (entry.ActorDisplayName != null && entry.ActorDisplayName.ToLower().Contains(term))
            || (entry.Summary != null && entry.Summary.ToLower().Contains(term)));
    }

    var total = await query.CountAsync(cancellationToken);
    var items = await query
        .OrderByDescending(entry => entry.OccurredAt)
        .Skip(offset)
        .Take(pageSize)
        .Select(entry => new AuditLogEntryResponse(
            entry.Id,
            entry.ActorType.ToString(),
            entry.ActorId,
            entry.ActorDisplayName,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.Summary,
            entry.BeforeJson,
            entry.AfterJson,
            entry.IpAddress,
            entry.UserAgent,
            entry.CorrelationId,
            entry.OccurredAt))
        .ToListAsync(cancellationToken);

    return Results.Ok(new AuditLogListResponse(total, offset, pageSize, items));
})
.WithName("ListAdminAuditLogs");

admin.MapGet("/company-applications", async (IAppDbContext db, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        var applications = await db.CompanyApplications
            .AsNoTracking()
            .OrderBy(application => application.Status == HomeService.Domain.Enums.CompanyApplicationStatus.Approved
                || application.Status == HomeService.Domain.Enums.CompanyApplicationStatus.ActivationSent
                || application.Status == HomeService.Domain.Enums.CompanyApplicationStatus.Activated)
            .ThenByDescending(application => application.SubmittedAt)
            .Select(application => new
            {
                application.Id,
                application.CompanyName,
                application.City,
                application.ContactName,
                application.Email,
                application.PhoneNumber,
                Status = application.Status.ToString(),
                application.SubmittedAt,
                application.LastReminderSentAt,
                application.ActivationEmailSentAt
            })
            .ToListAsync(cancellationToken);

        var applicationIds = applications.Select(application => application.Id).ToList();
        var documents = await db.CompanyApplicationDocuments
            .AsNoTracking()
            .Where(document => applicationIds.Contains(document.CompanyApplicationId))
            .OrderBy(document => document.DocumentType)
            .Select(document => new
            {
                document.CompanyApplicationId,
                document.Id,
                DocumentType = document.DocumentType.ToString(),
                ReviewStatus = document.ReviewStatus.ToString(),
                document.ReviewNote
            })
            .ToListAsync(cancellationToken);

        var documentsByApplication = documents
            .GroupBy(document => document.CompanyApplicationId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(document => new CompanyApplicationDocumentSummaryResponse(
                        document.Id,
                        document.DocumentType,
                        document.ReviewStatus,
                        document.ReviewNote))
                    .ToList());

        var response = applications
            .Select(application =>
            {
                documentsByApplication.TryGetValue(application.Id, out var applicationDocuments);
                applicationDocuments ??= [];

                return new CompanyApplicationSummaryResponse(
                    application.Id,
                    application.CompanyName,
                    application.City,
                    application.ContactName,
                    application.Email,
                    application.PhoneNumber,
                    application.Status,
                    application.SubmittedAt,
                    application.LastReminderSentAt,
                    application.ActivationEmailSentAt,
                    applicationDocuments.Count,
                    applicationDocuments.Count(document => document.ReviewStatus == HomeService.Domain.Enums.DocumentReviewStatus.Pending.ToString()),
                    applicationDocuments);
            })
            .ToList();

        return Results.Ok(response);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Unable to list company applications.");
        return Results.Problem(
            title: "Unable to list company applications",
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("ListCompanyApplications");

admin.MapGet("/notifications", async (IAppDbContext db, CancellationToken cancellationToken) =>
{
    var notifications = await db.NotificationOutboxMessages
        .AsNoTracking()
        .OrderBy(notification => notification.Status)
        .ThenByDescending(notification => notification.ScheduledAt)
        .Take(100)
        .Select(notification => new NotificationOutboxMessageResponse(
            notification.Id,
            notification.Channel.ToString(),
            notification.Status.ToString(),
            notification.Recipient,
            notification.Subject,
            notification.Body,
            notification.RelatedEntityType,
            notification.RelatedEntityId,
            notification.ScheduledAt,
            notification.SentAt,
            notification.FailureReason))
        .ToListAsync(cancellationToken);

    return Results.Ok(notifications);
})
.WithName("ListNotificationOutboxMessages");

admin.MapGet("/country-brandings/{countryCode}", async (string countryCode, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
    var branding = await db.CountryBrandings
        .AsNoTracking()
        .Where(branding => branding.Country!.IsoCode == normalizedCountryCode)
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
.WithName("GetAdminCountryBranding");

admin.MapPut("/country-brandings/{countryCode}", async (
    string countryCode,
    UpdateCountryBrandingRequest request,
    HttpRequest httpRequest,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var validationError = CountryBrandingValidator.Validate(request);
    if (validationError is not null)
    {
        return Results.BadRequest(new { message = validationError });
    }

    var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
    var country = await db.Countries.FirstOrDefaultAsync(country => country.IsoCode == normalizedCountryCode, cancellationToken);
    if (country is null)
    {
        return Results.NotFound(new { message = "Pays introuvable." });
    }

    var branding = await db.CountryBrandings.FirstOrDefaultAsync(branding => branding.CountryId == country.Id, cancellationToken);
    object? before = null;
    if (branding is null)
    {
        branding = new CountryBranding(
            country.Id,
            request.BrandName,
            request.PrimaryColor,
            request.SecondaryColor,
            request.AccentColor,
            request.HeroTitle,
            request.HeroSubtitle,
            request.HeroImageUrl,
            request.MotifStyle);
        db.CountryBrandings.Add(branding);
    }
    else
    {
        before = new
        {
            branding.BrandName,
            branding.PrimaryColor,
            branding.SecondaryColor,
            branding.AccentColor,
            branding.HeroTitle,
            branding.HeroSubtitle,
            branding.HeroImageUrl,
            branding.MotifStyle
        };
        branding.Update(
            request.BrandName,
            request.PrimaryColor,
            request.SecondaryColor,
            request.AccentColor,
            request.HeroTitle,
            request.HeroSubtitle,
            request.HeroImageUrl,
            request.MotifStyle);
    }

    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCountryBrandingUpdated",
        nameof(CountryBranding),
        branding.Id,
        $"Branding pays {country.IsoCode} mis a jour.",
        before,
        after: request);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(new CountryBrandingResponse(
        country.IsoCode,
        country.Name,
        branding.BrandName,
        branding.PrimaryColor,
        branding.SecondaryColor,
        branding.AccentColor,
        branding.HeroTitle,
        branding.HeroSubtitle,
        branding.HeroImageUrl,
        branding.MotifStyle));
})
.WithName("UpdateAdminCountryBranding");

admin.MapGet("/company-applications/{id:guid}", async (Guid id, IAppDbContext db, CancellationToken cancellationToken) =>
{
    var application = await db.CompanyApplications
        .AsNoTracking()
        .Where(application => application.Id == id)
        .Select(application => new CompanyApplicationDetailResponse(
            application.Id,
            application.CompanyId,
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
            application.ActivatedAt,
            application.Company == null ? null : application.Company.AssignmentMode.ToString(),
            application.ActivationTokens
                .OrderByDescending(token => token.CreatedAt)
                .Select(token => token.ActivationLink)
                .FirstOrDefault(),
            application.ActivationTokens
                .OrderByDescending(token => token.CreatedAt)
                .Select(token => (DateTimeOffset?)token.ExpiresAt)
                .FirstOrDefault(),
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
                .ToList(),
            application.StatusHistory
                .OrderBy(history => history.ChangedAt)
                .Select(history => new CompanyApplicationStatusHistoryResponse(
                    history.Id,
                    history.PreviousStatus == null ? null : history.PreviousStatus.ToString(),
                    history.NewStatus.ToString(),
                    history.Note,
                    history.ChangedBy,
                    history.ChangedAt))
                .ToList()))
        .FirstOrDefaultAsync(cancellationToken);

    return application is null ? Results.NotFound() : Results.Ok(application);
})
.WithName("GetCompanyApplication");

admin.MapPut("/companies/{id:guid}/assignment-mode", async (
    Guid id,
    UpdateCompanyAssignmentModeRequest request,
    HttpRequest httpRequest,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == id, cancellationToken);
    if (company is null)
    {
        return Results.NotFound();
    }

    if (!TryParseCompanyAssignmentMode(request.AssignmentMode, out var assignmentMode))
    {
        return Results.BadRequest(new { message = "Mode d'affectation invalide." });
    }

    var previousMode = company.AssignmentMode;
    company.ChangeAssignmentMode(assignmentMode);
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyAssignmentModeUpdated",
        nameof(Company),
        company.Id,
        "Mode d'affectation entreprise modifie.",
        before: new { AssignmentMode = previousMode },
        after: new { company.AssignmentMode });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(CompanyAssignmentModePresenter.ToResponse(company));
})
.WithName("UpdateCompanyAssignmentMode");

admin.MapPost("/company-applications/{id:guid}/approve", async (
    Guid id,
    HttpRequest httpRequest,
    AdminCompanyApplicationReviewService reviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await reviewService.ApproveAsync(id, cancellationToken);
    var error = ToAdminCompanyApplicationReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var application = result.Application!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationApproved",
        nameof(HomeService.Domain.Entities.CompanyApplication),
        application.Id,
        "Demande entreprise validee.",
        before: new { Status = result.PreviousStatus },
        after: new { application.Status, application.CompanyId });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("ApproveCompanyApplication");

admin.MapPost("/company-applications/{id:guid}/reject", async (
    Guid id,
    CompanyApplicationReviewRequest request,
    HttpRequest httpRequest,
    AdminCompanyApplicationReviewService reviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await reviewService.RejectAsync(id, request.Note, cancellationToken);
    var error = ToAdminCompanyApplicationReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var application = result.Application!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationRejected",
        nameof(HomeService.Domain.Entities.CompanyApplication),
        application.Id,
        "Demande entreprise refusee.",
        before: new { Status = result.PreviousStatus },
        after: new { application.Status, application.ReviewNote });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("RejectCompanyApplication");

admin.MapPost("/company-applications/{id:guid}/reopen", async (
    Guid id,
    CompanyApplicationReviewRequest request,
    HttpRequest httpRequest,
    AdminCompanyApplicationReviewService reviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await reviewService.ReopenAsync(id, request.Note, cancellationToken);
    var error = ToAdminCompanyApplicationReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var application = result.Application!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationReopened",
        nameof(HomeService.Domain.Entities.CompanyApplication),
        application.Id,
        "Demande entreprise reouverte.",
        before: new { Status = result.PreviousStatus },
        after: new { application.Status, application.ReviewNote });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("ReopenCompanyApplication");

admin.MapPost("/company-applications/{id:guid}/request-more-information", async (
    Guid id,
    CompanyApplicationReviewRequest request,
    HttpRequest httpRequest,
    AdminCompanyApplicationReviewService reviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await reviewService.RequestMoreInformationAsync(id, request.Note, cancellationToken);
    var error = ToAdminCompanyApplicationReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var application = result.Application!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationMoreInformationRequested",
        nameof(HomeService.Domain.Entities.CompanyApplication),
        application.Id,
        "Complement demande sur un dossier entreprise.",
        before: new { Status = result.PreviousStatus },
        after: new { application.Status, application.ReviewNote });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationActionResponse(application));
})
.WithName("RequestCompanyApplicationMoreInformation");

admin.MapPost("/company-applications/{id:guid}/activation-link", async (
    Guid id,
    HttpRequest httpRequest,
    CompanyActivationLinkGenerationService activationLinkService,
    IAppDbContext db,
    IConfiguration configuration,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await activationLinkService.GenerateAsync(
            id,
            GetCompanyPortalBaseUrl(httpRequest, configuration),
            GetActivationTokenDurationHours(configuration),
            "admin",
            cancellationToken);

        if (result.Status == CompanyActivationLinkGenerationStatus.NotFound)
        {
            return Results.NotFound(new { message = result.Message });
        }

        if (result.Status == CompanyActivationLinkGenerationStatus.InvalidStatus)
        {
            return Results.BadRequest(new { message = result.Message });
        }

        var response = result.Response!;
        AddAuditLog(
            db,
            httpRequest,
            AuditActor.Admin(),
            "AdminCompanyActivationLinkGenerated",
            nameof(HomeService.Domain.Entities.CompanyApplication),
            response.Id,
            "Lien d'activation entreprise genere.",
            before: new { Status = result.PreviousStatus },
            after: new { response.Status, response.ExpiresAt, response.ActivationLink });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(response);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Activation link generation failed for company application {ApplicationId}.", id);
        return Results.Problem(
            title: "Generation du lien d'activation impossible.",
            detail: exception.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GenerateCompanyApplicationActivationLink");

admin.MapPost("/company-application-documents/{id:guid}/approve", async (
    Guid id,
    HttpRequest httpRequest,
    AdminCompanyApplicationDocumentReviewService documentReviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await documentReviewService.ApproveAsync(id, cancellationToken);
    var error = ToAdminCompanyApplicationDocumentReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var document = result.Document!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationDocumentApproved",
        nameof(CompanyApplicationDocument),
        document.Id,
        "Piece entreprise validee.",
        before: new { Status = result.PreviousStatus },
        after: new { document.ReviewStatus });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("ApproveCompanyApplicationDocument");

admin.MapPost("/company-application-documents/{id:guid}/reject", async (
    Guid id,
    CompanyApplicationDocumentReviewRequest request,
    HttpRequest httpRequest,
    AdminCompanyApplicationDocumentReviewService documentReviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await documentReviewService.RejectAsync(id, request.Comment, cancellationToken);
    var error = ToAdminCompanyApplicationDocumentReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var document = result.Document!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationDocumentRejected",
        nameof(CompanyApplicationDocument),
        document.Id,
        "Piece entreprise refusee.",
        before: new { Status = result.PreviousStatus },
        after: new { document.ReviewStatus, document.ReviewNote });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("RejectCompanyApplicationDocument");

admin.MapPost("/company-application-documents/{id:guid}/request-replacement", async (
    Guid id,
    CompanyApplicationDocumentReviewRequest request,
    HttpRequest httpRequest,
    AdminCompanyApplicationDocumentReviewService documentReviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await documentReviewService.RequestReplacementAsync(id, request.Comment, cancellationToken);
    var error = ToAdminCompanyApplicationDocumentReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var document = result.Document!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationDocumentReplacementRequested",
        nameof(CompanyApplicationDocument),
        document.Id,
        "Remplacement de piece entreprise demande.",
        before: new { Status = result.PreviousStatus },
        after: new { document.ReviewStatus, document.ReviewNote });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("RequestCompanyApplicationDocumentReplacement");

admin.MapPost("/company-application-documents/{id:guid}/reopen", async (
    Guid id,
    CompanyApplicationDocumentReviewRequest request,
    HttpRequest httpRequest,
    AdminCompanyApplicationDocumentReviewService documentReviewService,
    IAppDbContext db,
    CancellationToken cancellationToken) =>
{
    var result = await documentReviewService.ReopenAsync(id, request.Comment, cancellationToken);
    var error = ToAdminCompanyApplicationDocumentReviewError(result);
    if (error is not null)
    {
        return error;
    }

    var document = result.Document!;
    AddAuditLog(
        db,
        httpRequest,
        AuditActor.Admin(),
        "AdminCompanyApplicationDocumentReopened",
        nameof(CompanyApplicationDocument),
        document.Id,
        "Piece entreprise reouverte.",
        before: new { Status = result.PreviousStatus },
        after: new { document.ReviewStatus, document.ReviewNote });
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(ToCompanyApplicationDocumentReviewResponse(document));
})
.WithName("ReopenCompanyApplicationDocument");

admin.MapGet("/company-application-documents/{id:guid}/download", async (
    Guid id,
    IAppDbContext db,
    CompanyApplicationUploadService uploadService,
    CancellationToken cancellationToken) =>
{
    var document = await db.CompanyApplicationDocuments
        .AsNoTracking()
        .Where(document => document.Id == id)
        .Select(document => new
        {
            document.OriginalFileName,
            document.StoragePath,
            document.ContentType
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (document is null)
    {
        return Results.NotFound();
    }

    string absolutePath;
    try
    {
        absolutePath = uploadService.GetAbsolutePath(document.StoragePath);
    }
    catch (InvalidOperationException)
    {
        return Results.BadRequest(new { message = "Chemin de document invalide." });
    }

    if (!File.Exists(absolutePath))
    {
        return Results.NotFound(new { message = "Le fichier n'existe plus sur le serveur." });
    }

    return Results.File(absolutePath, document.ContentType, document.OriginalFileName);
})
.WithName("DownloadCompanyApplicationDocument");

app.Run();

static ProviderMobileProfileCompletionResponse? BuildProviderMobileProfileCompletion(ProviderProfile provider)
{
    var missing = new List<string>();
    if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.Photo))
    {
        missing.Add("Photo de profil");
    }

    if (!provider.Documents.Any(document => document.DocumentType == ProviderDocumentType.IdentityDocument))
    {
        missing.Add("Piece d'identite");
    }

    if (!provider.Services.Any(service => service.IsActive))
    {
        missing.Add("Service actif");
    }

    if (provider.MissionLatitude is null || provider.MissionLongitude is null)
    {
        missing.Add("Zone de mission");
    }

    if (missing.Count == 0)
    {
        return null;
    }

    var percent = Math.Clamp(100 - missing.Count * 8, 0, 99);
    var message = missing.Count == 1
        ? $"Completez : {missing[0]}."
        : $"Completez {missing.Count} elements pour recevoir toutes les affectations.";

    return new ProviderMobileProfileCompletionResponse(percent, message, missing);
}

static ProviderMobileMissionSummaryResponse? ToProviderMobileMissionSummary(
    ProviderMissionAssignment assignment,
    IReadOnlyDictionary<Guid, Service> servicesById,
    IReadOnlyDictionary<Guid, CustomerProfile> customersById)
{
    if (assignment.Mission is null)
    {
        return null;
    }

    servicesById.TryGetValue(assignment.Mission.ServiceId, out var service);
    customersById.TryGetValue(assignment.Mission.CustomerId, out var customer);
    var canCallCustomer = assignment.Mission.CanRevealContactDetails && customer is not null;
    return new ProviderMobileMissionSummaryResponse(
        assignment.Id,
        assignment.MissionId,
        service?.Name ?? "Service",
        service?.IconName ?? "sparkles",
        assignment.Company?.Name ?? "Entreprise",
        BuildLocationLabel(assignment.Mission.ServiceAddress),
        assignment.Mission.ScheduledFor,
        assignment.Status.ToString(),
        canCallCustomer,
        canCallCustomer ? customer!.PhoneNumber : null);
}

static ProviderMobileMissionOfferResponse? ToProviderMobileMissionOffer(
    ProviderMissionAssignment assignment,
    ProviderProfile provider,
    DateTimeOffset now,
    IReadOnlyDictionary<Guid, Service> servicesById,
    IReadOnlyDictionary<Guid, CustomerProfile> customersById)
{
    if (assignment.Mission is null)
    {
        return null;
    }

    servicesById.TryGetValue(assignment.Mission.ServiceId, out var service);
    customersById.TryGetValue(assignment.Mission.CustomerId, out var customer);
    var distanceKm = CalculateDistanceKm(
        provider.CurrentLatitude ?? provider.MissionLatitude,
        provider.CurrentLongitude ?? provider.MissionLongitude,
        assignment.Mission.ServiceLatitude,
        assignment.Mission.ServiceLongitude);

    return new ProviderMobileMissionOfferResponse(
        assignment.Id,
        assignment.MissionId,
        service?.Name ?? "Service",
        service?.IconName ?? "sparkles",
        assignment.Company?.Name ?? provider.Company?.Name ?? "Entreprise",
        BuildCustomerDisplayName(customer),
        BuildLocationLabel(assignment.Mission.ServiceAddress),
        distanceKm,
        distanceKm is null ? null : Math.Max(1, (int)Math.Round(distanceKm.Value / 18d * 60d)),
        assignment.ExpiresAt,
        Math.Max(0, (int)Math.Floor((assignment.ExpiresAt - now).TotalSeconds)),
        "Verifiez que vous pouvez partir maintenant avant d'accepter.");
}

static string BuildLocationLabel(string? address)
{
    return string.IsNullOrWhiteSpace(address) ? "Adresse a confirmer" : address.Trim();
}

static string BuildCustomerDisplayName(CustomerProfile? customer)
{
    if (customer is null)
    {
        return "Client";
    }

    var displayName = $"{customer.FirstName} {customer.LastName}".Trim();
    return string.IsNullOrWhiteSpace(displayName) ? "Client" : displayName;
}

static double? CalculateDistanceKm(decimal? fromLatitude, decimal? fromLongitude, decimal? toLatitude, decimal? toLongitude)
{
    if (fromLatitude is null || fromLongitude is null || toLatitude is null || toLongitude is null)
    {
        return null;
    }

    const double earthRadiusKm = 6371d;
    var latA = DegreesToRadians((double)fromLatitude.Value);
    var latB = DegreesToRadians((double)toLatitude.Value);
    var deltaLatitude = DegreesToRadians((double)(toLatitude.Value - fromLatitude.Value));
    var deltaLongitude = DegreesToRadians((double)(toLongitude.Value - fromLongitude.Value));
    var haversine = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2)
        + Math.Cos(latA) * Math.Cos(latB) * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
    var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    return Math.Round(earthRadiusKm * centralAngle, 1);
}

static double DegreesToRadians(double degrees)
{
    return degrees * Math.PI / 180d;
}

static async Task<ProviderPortalSession?> GetProviderPortalSessionAsync(
    HttpRequest request,
    IAppDbContext db,
    CancellationToken cancellationToken)
{
    var authorization = request.Headers.Authorization.ToString();
    const string bearerPrefix = "Bearer ";
    if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var token = authorization[bearerPrefix.Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
        return null;
    }

    var tokenHash = PortalTokenService.HashToken(token);
    return await db.ProviderPortalSessions
        .Include(session => session.Provider)
        .ThenInclude(provider => provider!.Company)
        .FirstOrDefaultAsync(session => session.TokenHash == tokenHash && session.RevokedAt == null && session.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
}

static IResult ToProviderMissionHttpResult(ProviderMissionOperationResult result)
{
    return result.Status switch
    {
        ProviderMissionOperationStatus.Ok => Results.Ok(result.Response),
        ProviderMissionOperationStatus.Forbidden => Results.Forbid(),
        ProviderMissionOperationStatus.NotFound => Results.NotFound(new { message = result.Message }),
        ProviderMissionOperationStatus.BadRequest => result.Response is null
            ? Results.BadRequest(new { message = result.Message })
            : Results.BadRequest(result.Response),
        _ => Results.BadRequest(new { message = result.Message ?? "Action impossible." })
    };
}

static string GetFormValue(IFormCollection form, string key)
{
    return form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
}

static string? GetOptionalFormValue(IFormCollection form, string key)
{
    var value = GetFormValue(form, key);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static int? GetOptionalInt(IFormCollection form, string key)
{
    return int.TryParse(GetFormValue(form, key), out var value) ? value : null;
}

static IReadOnlyList<string> GetServices(IFormCollection form)
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

static IReadOnlyList<Guid> GetGuidValues(IFormCollection form, string key)
{
    if (!form.TryGetValue(key, out var values))
    {
        return [];
    }

    return values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
        .Where(value => value != Guid.Empty)
        .Distinct()
        .ToList();
}

static decimal? GetOptionalDecimal(IFormCollection form, string key)
{
    return decimal.TryParse(GetFormValue(form, key), out var value) ? value : null;
}

static ExperienceLevel ParseExperienceLevel(string? value)
{
    return Enum.TryParse<ExperienceLevel>(value, true, out var level)
        ? level
        : ExperienceLevel.Confirmed;
}

static ProviderEmploymentType ParseProviderEmploymentType(string? value)
{
    return Enum.TryParse<ProviderEmploymentType>(value, true, out var employmentType)
        ? employmentType
        : ProviderEmploymentType.CompanyEmployee;
}

static ProviderGender ParseProviderGender(string? value)
{
    return Enum.TryParse<ProviderGender>(value, true, out var gender)
        ? gender
        : ProviderGender.Unspecified;
}

static bool TryParseCompanyAssignmentMode(string? value, out CompanyAssignmentMode assignmentMode)
{
    return Enum.TryParse(value?.Trim(), true, out assignmentMode);
}

static CompanyProviderFormData ToCompanyProviderFormData(IFormCollection form)
{
    return new CompanyProviderFormData(
        GetFormValue(form, "firstName"),
        GetFormValue(form, "lastName"),
        GetFormValue(form, "phoneNumber"),
        DateOnly.TryParse(GetFormValue(form, "dateOfBirth"), out var birthDate) ? birthDate : null,
        GetFormValue(form, "address"),
        GetOptionalInt(form, "yearsOfExperience"),
        GetOptionalInt(form, "missionRadiusKm"),
        GetGuidValues(form, "serviceIds"),
        form.Files.GetFile("photo") is not null,
        form.Files.GetFile("identityDocument") is not null);
}

static CompanyApplicationActionResponse ToCompanyApplicationActionResponse(HomeService.Domain.Entities.CompanyApplication application)
{
    return new CompanyApplicationActionResponse(
        application.Id,
        application.Status.ToString(),
        application.ReviewedAt,
        application.ReviewNote);
}

static IResult? ToAdminCompanyApplicationReviewError(AdminCompanyApplicationReviewResult result)
{
    return result.Status switch
    {
        AdminCompanyApplicationReviewStatus.Ok => null,
        AdminCompanyApplicationReviewStatus.NotFound => Results.NotFound(),
        AdminCompanyApplicationReviewStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
        AdminCompanyApplicationReviewStatus.MissingRequiredApprovedDocuments => Results.BadRequest(new { message = result.Message }),
        AdminCompanyApplicationReviewStatus.InvalidTransition => Results.BadRequest(new { message = result.Message }),
        _ => Results.BadRequest(new { message = result.Message ?? "Action impossible." })
    };
}

static IResult? ToAdminCompanyApplicationDocumentReviewError(AdminCompanyApplicationDocumentReviewResult result)
{
    return result.Status switch
    {
        AdminCompanyApplicationDocumentReviewStatus.Ok => null,
        AdminCompanyApplicationDocumentReviewStatus.NotFound => Results.NotFound(),
        AdminCompanyApplicationDocumentReviewStatus.ValidationFailed => Results.BadRequest(new { message = result.Message }),
        AdminCompanyApplicationDocumentReviewStatus.InvalidTransition => Results.BadRequest(new { message = result.Message }),
        _ => Results.BadRequest(new { message = result.Message ?? "Action impossible." })
    };
}

static CompanyApplicationDocumentReviewResponse ToCompanyApplicationDocumentReviewResponse(CompanyApplicationDocument document)
{
    return new CompanyApplicationDocumentReviewResponse(
        document.Id,
        document.CompanyApplicationId,
        document.ReviewStatus.ToString(),
        document.ReviewNote);
}

static string GetCompanyPortalBaseUrl(HttpRequest request, IConfiguration configuration)
{
    var configuredBaseUrl =
        configuration["CompanyPortal:BaseUrl"]
        ?? configuration["COMPANY_PORTAL_BASE_URL"]
        ?? configuration["CompanyPortalBaseUrl"];

    return string.IsNullOrWhiteSpace(configuredBaseUrl)
        ? $"{request.Scheme}://{request.Host}"
        : configuredBaseUrl.Trim();
}

static int GetActivationTokenDurationHours(IConfiguration configuration)
{
    var configuredValue = configuration["CompanyPortal:ActivationTokenHours"] ?? configuration["COMPANY_ACTIVATION_TOKEN_HOURS"];
    return CompanyActivationTokenLifetimeResolver.ResolveHours(configuredValue);
}

static void AddAuditLog(
    IAppDbContext db,
    HttpRequest request,
    AuditActor actor,
    string action,
    string entityType,
    Guid? entityId,
    string? summary,
    object? before = null,
    object? after = null)
{
    db.AuditLogEntries.Add(AuditLogFactory.Create(
        actor,
        action,
        entityType,
        entityId,
        summary,
        GetAuditRequestContext(request),
        before,
        after));
}

static AuditRequestContext GetAuditRequestContext(HttpRequest request)
{
    return HttpAuditContextFactory.Create(request);
}

public partial class Program;
