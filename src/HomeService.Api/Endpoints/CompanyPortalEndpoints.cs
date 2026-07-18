using HomeService.Api.Auditing;
using HomeService.Application.Companies;
using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.CompanyPortal;
using HomeService.Contracts.CompanyPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HomeService.Api.Endpoints;

public static class CompanyPortalEndpoints
{
    public static IEndpointRouteBuilder MapCompanyPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/company-portal");

        group.MapPost("/login", async (
            CompanyPortalLoginRequest request,
            HttpRequest httpRequest,
            CompanyPortalAuthService authService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            var error = CompanyPortalLoginHttpMapper.ToErrorResult(result);
            if (error is not null)
            {
                return error;
            }

            var response = result.Response!;
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(response.CompanyId, response.CompanyName),
                "CompanyPortalLoginSucceeded",
                nameof(CompanyPortalSession),
                result.Session!.Id,
                "Connexion au portail entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { response.Email, response.CompanyId, request.RememberMe, response.ExpiresAt }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(response);
        })
        .WithName("LoginCompanyPortal");

        group.MapGet("/{companyId:guid}/interim-candidates", async (
            Guid companyId,
            CompanyInterimCandidateService interimCandidateService,
            CancellationToken cancellationToken) =>
        {
            var candidates = await interimCandidateService.ListAsync(companyId, cancellationToken);
            return Results.Ok(candidates);
        })
        .WithName("ListCompanyPortalInterimCandidates");

        group.MapGet("/{companyId:guid}/interim-settings", async (
            Guid companyId,
            CompanyInterimCandidateService interimCandidateService,
            CancellationToken cancellationToken) =>
        {
            var result = await interimCandidateService.GetSettingsAsync(companyId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetCompanyPortalInterimSettings");

        group.MapPut("/{companyId:guid}/interim-settings", async (
            Guid companyId,
            UpdateCompanyInterimSettingsRequest request,
            HttpRequest httpRequest,
            CompanyInterimCandidateService interimCandidateService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await interimCandidateService.UpdateSettingsAsync(companyId, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return Results.NotFound(new { message = result.Message });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, "Entreprise"),
                "CompanyInterimSettingsUpdated",
                nameof(Company),
                companyId,
                "Preference de reception des candidatures interimaires modifiee.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { request.AcceptsInterimApplications }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(result.Response);
        })
        .WithName("UpdateCompanyPortalInterimSettings");

        group.MapGet("/{companyId:guid}/dashboard", async (
            Guid companyId,
            CompanyPortalDashboardService dashboardService,
            CancellationToken cancellationToken) =>
        {
            var result = await dashboardService.GetAsync(companyId, userId: null, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetCompanyPortalDashboard");

        group.MapPost("/{companyId:guid}/interim-candidates/{requestId:guid}/approve", async (
            Guid companyId,
            Guid requestId,
            CompanyReviewInterimCandidateRequest request,
            HttpRequest httpRequest,
            CompanyInterimCandidateService interimCandidateService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await interimCandidateService.ApproveAsync(companyId, requestId, request.Note, cancellationToken);
            if (result.IsNotFound)
            {
                return Results.NotFound(new { message = "Demande d'interim introuvable." });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, "Entreprise"),
                "InterimCandidateApproved",
                nameof(ProviderAffiliationRequest),
                requestId,
                "Demandeur d'interim valide par l'entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { companyId, requestId, request.Note }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Candidat valide comme interimaire. Il devient assignable apres rattachement entreprise." });
        })
        .WithName("ApproveCompanyPortalInterimCandidate");

        group.MapPost("/{companyId:guid}/interim-candidates/{requestId:guid}/reject", async (
            Guid companyId,
            Guid requestId,
            CompanyReviewInterimCandidateRequest request,
            HttpRequest httpRequest,
            CompanyInterimCandidateService interimCandidateService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await interimCandidateService.RejectAsync(companyId, requestId, request.Note, cancellationToken);
            if (result.IsNotFound)
            {
                return Results.NotFound(new { message = "Demande d'interim introuvable." });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, "Entreprise"),
                "InterimCandidateRejected",
                nameof(ProviderAffiliationRequest),
                requestId,
                "Demandeur d'interim refuse par l'entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { companyId, requestId, request.Note }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Candidature refusee." });
        })
        .WithName("RejectCompanyPortalInterimCandidate");

        group.MapPost("/{companyId:guid}/compliance-documents", async (
            Guid companyId,
            HttpRequest httpRequest,
            CompanyComplianceDocumentService complianceDocumentService,
            IAppDbContext db,
            CompanyApplicationUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Les documents doivent etre envoyes au format multipart/form-data." });
            }

            var target = await complianceDocumentService.GetUploadTargetAsync(companyId, cancellationToken);
            if (target.Status == CompanyComplianceDocumentStatus.CompanyNotFound)
            {
                return Results.NotFound(new { message = target.Message });
            }

            if (target.Status == CompanyComplianceDocumentStatus.ApplicationNotFound || target.ApplicationId is null)
            {
                return Results.NotFound(new { message = target.Message });
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var documents = await uploadService.SaveAsync(target.ApplicationId.Value, form.Files, cancellationToken);
            var result = await complianceDocumentService.AttachDocumentsAsync(
                companyId,
                documents.Select(document => new CompanyApplicationUploadedDocument(
                        document.DocumentType,
                        document.OriginalFileName,
                        document.StoragePath,
                        document.ContentType))
                    .ToList(),
                cancellationToken);

            if (result.Status == CompanyComplianceDocumentStatus.NoValidDocument)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            if (result.Status == CompanyComplianceDocumentStatus.CompanyNotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            if (result.Status == CompanyComplianceDocumentStatus.ApplicationNotFound || result.ApplicationId is null)
            {
                return Results.NotFound(new { message = result.Message });
            }

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, null),
                "CompanyComplianceDocumentsUploaded",
                nameof(HomeService.Domain.Entities.CompanyApplication),
                result.ApplicationId.Value,
                "Pieces de conformite ajoutees depuis le portail entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { result.DocumentCount, result.DocumentTypes }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = result.Message, count = result.DocumentCount });
        })
        .WithName("UploadCompanyPortalComplianceDocuments");

        group.MapGet("/{companyId:guid}/profile", async (
            Guid companyId,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.GetProfileAsync(companyId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Response)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetCompanyPortalProfile");

        group.MapPut("/{companyId:guid}/profile/company", async (
            Guid companyId,
            UpdateCompanyPortalCompanyInfoRequest request,
            HttpRequest httpRequest,
            CompanyPortalProfileManagementService profileService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await profileService.UpdateCompanyInformationAsync(companyId, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return result.IsNotFound
                    ? Results.NotFound(new { message = result.Message })
                    : Results.BadRequest(new { message = result.Message });
            }

            AddCompanyProfileAudit(db, httpRequest, companyId, "CompanyPortalCompanyInformationUpdated", "Informations entreprise modifiees.", after: request);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalCompanyInformation");

        group.MapPut("/{companyId:guid}/profile/contact", async (
            Guid companyId,
            UpdateCompanyPortalContactRequest request,
            HttpRequest httpRequest,
            CompanyPortalProfileManagementService profileService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await profileService.UpdateContactAsync(companyId, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return result.IsNotFound
                    ? Results.NotFound(new { message = result.Message })
                    : Results.BadRequest(new { message = result.Message });
            }

            AddCompanyProfileAudit(db, httpRequest, companyId, "CompanyPortalContactUpdated", "Responsable entreprise modifie.", after: request);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalContact");

        group.MapPut("/{companyId:guid}/profile/operations", async (
            Guid companyId,
            UpdateCompanyPortalOperationsRequest request,
            HttpRequest httpRequest,
            CompanyPortalProfileManagementService profileService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await profileService.UpdateOperationsAsync(companyId, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return result.IsNotFound
                    ? Results.NotFound(new { message = result.Message })
                    : Results.BadRequest(new { message = result.Message });
            }

            AddCompanyProfileAudit(db, httpRequest, companyId, "CompanyPortalOperationsUpdated", "Zones et services entreprise modifies.", after: request);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalOperations");

        group.MapPut("/{companyId:guid}/profile/payment", async (
            Guid companyId,
            UpdateCompanyPortalPaymentRequest request,
            HttpRequest httpRequest,
            CompanyPortalProfileManagementService profileService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await profileService.UpdatePaymentAsync(companyId, request, cancellationToken);
            if (!result.IsSuccess)
            {
                return result.IsNotFound
                    ? Results.NotFound(new { message = result.Message })
                    : Results.BadRequest(new { message = result.Message });
            }

            AddCompanyProfileAudit(db, httpRequest, companyId, "CompanyPortalPaymentUpdated", "Coordonnees de paiement modifiees.", after: request);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalPayment");

        group.MapGet("/{companyId:guid}/missions", async (
            Guid companyId,
            string? view,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.ListMissionsAsync(companyId, view, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Missions)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListCompanyPortalMissions");

        group.MapGet("/{companyId:guid}/missions/{missionId:guid}/assignable-providers", async (
            Guid companyId,
            Guid missionId,
            CompanyMissionAssignmentService assignmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await assignmentService.ListAssignableProvidersAsync(companyId, missionId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Providers)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListCompanyPortalAssignableProviders");

        group.MapPost("/{companyId:guid}/missions/{missionId:guid}/assign", async (
            Guid companyId,
            Guid missionId,
            AssignCompanyMissionRequest request,
            CompanyMissionAssignmentService assignmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await assignmentService.AssignAsync(
                companyId,
                missionId,
                request.ProviderId,
                request.QuotedAmount,
                request.OverMaxJustification,
                cancellationToken);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Response);
            }

            return result.IsNotFound
                ? Results.NotFound(new { message = result.Message })
                : Results.BadRequest(new { message = result.Message });
        })
        .WithName("AssignCompanyPortalMission");

        group.MapGet("/{companyId:guid}/employees", async (
            Guid companyId,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.ListEmployeesAsync(companyId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Employees)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("ListCompanyPortalEmployees");

        group.MapPost("/{companyId:guid}/employees", async (
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
                GetOptionalFormValue(form, "email"),
                DateOnly.Parse(GetFormValue(form, "dateOfBirth")),
                GetFormValue(form, "address"),
                ParseProviderGender(GetOptionalFormValue(form, "gender")),
                ParseProviderEmploymentType(GetOptionalFormValue(form, "employmentType")),
                GetOptionalInt(form, "yearsOfExperience") ?? 0,
                GetOptionalDecimal(form, "missionLatitude"),
                GetOptionalDecimal(form, "missionLongitude"),
                GetOptionalInt(form, "missionRadiusKm") ?? 5);

            var requestedServices = GetEmployeeServiceSelections(form);
            var requestedServiceIds = requestedServices.Select(service => service.ServiceId).Distinct().ToList();
            var services = await db.Services
                .Where(service => requestedServiceIds.Contains(service.Id) && service.IsActive)
                .Select(service => service.Id)
                .ToListAsync(cancellationToken);

            var activePrestations = await db.ServicePrestations
                .Where(prestation => services.Contains(prestation.ServiceId) && prestation.IsActive)
                .Select(prestation => new { prestation.Id, prestation.ServiceId })
                .ToListAsync(cancellationToken);

            var activePrestationsByService = activePrestations
                .GroupBy(prestation => prestation.ServiceId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(prestation => prestation.Id).ToHashSet());

            provider.SyncCompanyServices(requestedServices
                .Where(service => services.Contains(service.ServiceId))
                .Select(service => (
                    service.ServiceId,
                    ParseExperienceLevel(service.ExperienceLevel),
                    Math.Max(0, service.YearsOfExperience),
                    ParseProviderServicePriceTier(service.PriceTier))));

            foreach (var providerService in provider.Services.Where(service => service.IsActive))
            {
                var requestService = requestedServices.LastOrDefault(service => service.ServiceId == providerService.ServiceId);
                if (requestService is null)
                {
                    continue;
                }

                var allowedPrestationIds = activePrestationsByService.TryGetValue(providerService.ServiceId, out var ids)
                    ? ids
                    : new HashSet<Guid>();
                providerService.SyncPrestations(requestService.ServicePrestationIds
                    .Where(allowedPrestationIds.Contains));
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

            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(company.Id, company.Name),
                "CompanyEmployeeCreated",
                nameof(ProviderProfile),
                provider.Id,
                "Prestataire ajoute depuis le portail entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new
                {
                    provider.FirstName,
                    provider.LastName,
                    provider.PhoneNumber,
                    provider.Email,
                    provider.EmploymentType,
                    provider.Gender,
                    provider.YearsOfExperience,
                    ServiceCount = services.Count,
                    DocumentCount = documents.Count
                }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/company-portal/{companyId}/employees/{provider.Id}",
                new CreateCompanyEmployeeResult(provider.Id, invitation.Code, invitation.InvitationLink, invitation.ExpiresAt));
        })
        .WithName("CreateCompanyPortalEmployee");

        group.MapPut("/{companyId:guid}/employees/{employeeId:guid}", async (
            Guid companyId,
            Guid employeeId,
            UpdateCompanyEmployeeRequest request,
            HttpRequest httpRequest,
            CompanyEmployeeManagementService employeeManagementService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await employeeManagementService.UpdateProfileAsync(companyId, employeeId, request, cancellationToken);
            if (result.Status == CompanyEmployeeOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            AddCompanyEmployeeAudit(
                db,
                httpRequest,
                companyId,
                "CompanyEmployeeUpdated",
                result.Provider!.Id,
                "Profil prestataire modifie depuis le portail entreprise.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalEmployee");

        group.MapPut("/{companyId:guid}/employees/{employeeId:guid}/services", async (
            Guid companyId,
            Guid employeeId,
            UpdateCompanyEmployeeServicesRequest request,
            HttpRequest httpRequest,
            CompanyEmployeeManagementService employeeManagementService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await employeeManagementService.UpdateServicesAsync(companyId, employeeId, request, cancellationToken);
            if (result.Status == CompanyEmployeeOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            AddCompanyEmployeeAudit(
                db,
                httpRequest,
                companyId,
                "CompanyEmployeeServicesUpdated",
                result.Provider!.Id,
                "Services prestataire mis a jour.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalEmployeeServices");

        group.MapPost("/{companyId:guid}/employees/{employeeId:guid}/availability", async (
            Guid companyId,
            Guid employeeId,
            UpdateCompanyEmployeeAvailabilityRequest request,
            HttpRequest httpRequest,
            CompanyEmployeeManagementService employeeManagementService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await employeeManagementService.UpdateAvailabilityAsync(companyId, employeeId, request, cancellationToken);
            if (result.Status == CompanyEmployeeOperationStatus.NotFound)
            {
                return Results.NotFound(new { message = result.Message });
            }

            AddCompanyEmployeeAudit(
                db,
                httpRequest,
                companyId,
                "CompanyEmployeeAvailabilityUpdated",
                result.Provider!.Id,
                request.IsAvailable ? "Prestataire marque disponible." : "Prestataire marque indisponible.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("UpdateCompanyPortalEmployeeAvailability");

        group.MapPost("/{companyId:guid}/employees/{employeeId:guid}/suspend", async (
            Guid companyId,
            Guid employeeId,
            HttpRequest httpRequest,
            CompanyEmployeeManagementService employeeManagementService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await employeeManagementService.SuspendAsync(companyId, employeeId, cancellationToken);
            if (result.Status == CompanyEmployeeOperationStatus.NotFound)
            {
                return Results.NotFound();
            }

            AddCompanyEmployeeAudit(
                db,
                httpRequest,
                companyId,
                "CompanyEmployeeSuspended",
                result.Provider!.Id,
                "Prestataire suspendu par l'entreprise.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("SuspendCompanyPortalEmployee");

        group.MapDelete("/{companyId:guid}/employees/{employeeId:guid}", async (
            Guid companyId,
            Guid employeeId,
            HttpRequest httpRequest,
            CompanyEmployeeManagementService employeeManagementService,
            IAppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var result = await employeeManagementService.DeactivateAsync(companyId, employeeId, cancellationToken);
            if (result.Status == CompanyEmployeeOperationStatus.NotFound)
            {
                return Results.NotFound();
            }

            AddCompanyEmployeeAudit(
                db,
                httpRequest,
                companyId,
                "CompanyEmployeeDeactivated",
                result.Provider!.Id,
                "Prestataire desactive par l'entreprise.",
                result.Before,
                result.After);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeactivateCompanyPortalEmployee");

        group.MapPost("/{companyId:guid}/employees/{employeeId:guid}/invitation-code", async (
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
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, null),
                "ProviderInvitationCodeGenerated",
                nameof(ProviderInvitation),
                invitation.Id,
                "Code d'acces prestataire genere depuis le portail entreprise.",
                HttpAuditContextFactory.Create(httpRequest),
                after: new { ProviderId = result.ProviderId, invitation.Code, invitation.ExpiresAt }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new CreateCompanyEmployeeResult(result.ProviderId!.Value, invitation.Code, invitation.InvitationLink, invitation.ExpiresAt));
        })
        .WithName("GenerateCompanyPortalEmployeeInvitationCode");

        group.MapPost("/{companyId:guid}/employees/{employeeId:guid}/documents", async (
            Guid companyId,
            Guid employeeId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "La piece doit etre envoyee au format multipart/form-data." });
            }

            var provider = await db.Providers
                .Include(provider => provider.Documents)
                .FirstOrDefaultAsync(provider => provider.Id == employeeId && provider.CompanyId == companyId, cancellationToken);
            if (provider is null)
            {
                return Results.NotFound(new { message = "Employe introuvable." });
            }

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            if (!TryParseProviderDocumentType(GetOptionalFormValue(form, "documentType"), out var documentType))
            {
                return Results.BadRequest(new { message = "Type de piece invalide." });
            }

            var file = form.Files.GetFile("file");
            if (file is null)
            {
                return Results.BadRequest(new { message = "Aucun fichier recu." });
            }

            var oldDocuments = provider.Documents.Where(document => document.DocumentType == documentType).ToList();
            foreach (var oldDocument in oldDocuments)
            {
                TryDeleteProviderFile(uploadService, oldDocument.StoragePath);
                db.ProviderDocuments.Remove(oldDocument);
            }

            var stored = await uploadService.SaveOneAsync(companyId, provider.Id, documentType, file, cancellationToken);
            provider.AttachDocument(new ProviderDocument(provider.Id, stored.DocumentType, stored.OriginalFileName, stored.StoragePath, stored.ContentType));
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, null),
                "CompanyEmployeeDocumentUploaded",
                nameof(ProviderDocument),
                null,
                "Piece prestataire ajoutee ou remplacee.",
                HttpAuditContextFactory.Create(httpRequest),
                before: new { ReplacedDocumentCount = oldDocuments.Count, DocumentType = documentType },
                after: new { stored.DocumentType, stored.OriginalFileName, stored.ContentType }));
            await db.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .WithName("UploadCompanyPortalEmployeeDocument");

        group.MapDelete("/{companyId:guid}/employees/{employeeId:guid}/documents/{documentId:guid}", async (
            Guid companyId,
            Guid employeeId,
            Guid documentId,
            HttpRequest httpRequest,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var document = await db.ProviderDocuments
                .Include(document => document.Provider)
                .FirstOrDefaultAsync(document => document.Id == documentId && document.ProviderId == employeeId && document.Provider!.CompanyId == companyId, cancellationToken);
            if (document is null)
            {
                return Results.NotFound(new { message = "Piece introuvable." });
            }

            TryDeleteProviderFile(uploadService, document.StoragePath);
            db.ProviderDocuments.Remove(document);
            db.AuditLogEntries.Add(AuditLogFactory.Create(
                AuditActor.Company(companyId, null),
                "CompanyEmployeeDocumentDeleted",
                nameof(ProviderDocument),
                document.Id,
                "Piece prestataire supprimee.",
                HttpAuditContextFactory.Create(httpRequest),
                before: new { document.DocumentType, document.OriginalFileName, document.ContentType }));
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteCompanyPortalEmployeeDocument");

        group.MapGet("/{companyId:guid}/payments", async (
            Guid companyId,
            string? period,
            CompanyPortalQueryService queryService,
            CancellationToken cancellationToken) =>
        {
            var result = await queryService.GetPaymentsAsync(companyId, period, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Summary)
                : Results.NotFound(new { message = result.Message });
        })
        .WithName("GetCompanyPortalPayments");

        group.MapGet("/provider-documents/{id:guid}/preview", async (
            Guid id,
            IAppDbContext db,
            CompanyProviderUploadService uploadService,
            CancellationToken cancellationToken) =>
        {
            var document = await db.ProviderDocuments
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

            var absolutePath = uploadService.GetAbsolutePath(document.StoragePath);
            if (!File.Exists(absolutePath))
            {
                return Results.NotFound(new { message = "Le fichier employe n'existe plus sur le serveur." });
            }

            return Results.File(absolutePath, document.ContentType, document.OriginalFileName, enableRangeProcessing: true);
        })
        .WithName("PreviewCompanyPortalProviderDocument");

        return app;
    }

    private static void AddCompanyEmployeeAudit(
        IAppDbContext db,
        HttpRequest httpRequest,
        Guid companyId,
        string action,
        Guid providerId,
        string description,
        object? before,
        object? after)
    {
        db.AuditLogEntries.Add(AuditLogFactory.Create(
            AuditActor.Company(companyId, null),
            action,
            nameof(ProviderProfile),
            providerId,
            description,
            HttpAuditContextFactory.Create(httpRequest),
            before,
            after));
    }

    private static void AddCompanyProfileAudit(
        IAppDbContext db,
        HttpRequest httpRequest,
        Guid companyId,
        string action,
        string description,
        object? after)
    {
        db.AuditLogEntries.Add(AuditLogFactory.Create(
            AuditActor.Company(companyId, null),
            action,
            nameof(Company),
            companyId,
            description,
            HttpAuditContextFactory.Create(httpRequest),
            after: after));
    }

    private static string? GetOptionalFormValue(IFormCollection form, string key)
    {
        var value = GetFormValue(form, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string GetFormValue(IFormCollection form, string key)
    {
        return form.TryGetValue(key, out var value) ? value.ToString() : string.Empty;
    }

    private static int? GetOptionalInt(IFormCollection form, string key)
    {
        return int.TryParse(GetFormValue(form, key), out var value) ? value : null;
    }

    private static IReadOnlyList<Guid> GetGuidValues(IFormCollection form, string key)
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

    private static IReadOnlyList<CompanyPortalCreateEmployeeServiceRequest> GetEmployeeServiceSelections(IFormCollection form)
    {
        var rawSelections = GetOptionalFormValue(form, "serviceSelections");
        if (!string.IsNullOrWhiteSpace(rawSelections))
        {
            try
            {
                var selections = JsonSerializer.Deserialize<IReadOnlyList<CompanyPortalCreateEmployeeServiceRequest>>(
                        rawSelections,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? [];

                if (selections.Count > 0)
                {
                    return selections;
                }
            }
            catch (JsonException)
            {
            }
        }

        return GetGuidValues(form, "serviceIds")
            .Select(serviceId => new CompanyPortalCreateEmployeeServiceRequest(
                serviceId,
                GetOptionalFormValue(form, "experienceLevel") ?? nameof(ExperienceLevel.Confirmed),
                GetOptionalInt(form, "yearsOfExperience") ?? 0,
                nameof(ProviderServicePriceTier.Normal),
                []))
            .ToList();
    }

    private static decimal? GetOptionalDecimal(IFormCollection form, string key)
    {
        return decimal.TryParse(GetFormValue(form, key), out var value) ? value : null;
    }

    private static bool TryParseProviderDocumentType(string? value, out ProviderDocumentType documentType)
    {
        return Enum.TryParse(value?.Trim(), true, out documentType);
    }

    private static ExperienceLevel ParseExperienceLevel(string? value)
    {
        return Enum.TryParse<ExperienceLevel>(value, true, out var level)
            ? level
            : ExperienceLevel.Confirmed;
    }

    private static ProviderEmploymentType ParseProviderEmploymentType(string? value)
    {
        return Enum.TryParse<ProviderEmploymentType>(value, true, out var employmentType)
            ? employmentType
            : ProviderEmploymentType.CompanyEmployee;
    }

    private static ProviderGender ParseProviderGender(string? value)
    {
        return Enum.TryParse<ProviderGender>(value, true, out var gender)
            ? gender
            : ProviderGender.Unspecified;
    }

    private static ProviderServicePriceTier ParseProviderServicePriceTier(string? value)
    {
        return Enum.TryParse<ProviderServicePriceTier>(value, true, out var tier)
            ? tier
            : ProviderServicePriceTier.Normal;
    }

    private static CompanyProviderFormData ToCompanyProviderFormData(IFormCollection form)
    {
        return new CompanyProviderFormData(
            GetFormValue(form, "firstName"),
            GetFormValue(form, "lastName"),
            GetFormValue(form, "phoneNumber"),
            GetOptionalFormValue(form, "email"),
            DateOnly.TryParse(GetFormValue(form, "dateOfBirth"), out var birthDate) ? birthDate : null,
            GetFormValue(form, "address"),
            GetOptionalInt(form, "yearsOfExperience"),
            GetOptionalInt(form, "missionRadiusKm"),
            GetGuidValues(form, "serviceIds"),
            form.Files.GetFile("photo") is not null,
            form.Files.GetFile("identityDocument") is not null);
    }

    private static void TryDeleteProviderFile(CompanyProviderUploadService uploadService, string storagePath)
    {
        try
        {
            var absolutePath = uploadService.GetAbsolutePath(storagePath);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public static class CompanyPortalLoginHttpMapper
{
    public static IResult? ToErrorResult(CompanyPortalLoginResult result)
    {
        return result.Status switch
        {
            CompanyPortalLoginStatus.Ok => null,
            CompanyPortalLoginStatus.MissingCredentials => Results.BadRequest(new { message = result.Message }),
            CompanyPortalLoginStatus.InvalidCredentials => Results.Unauthorized(),
            CompanyPortalLoginStatus.CompanySuspended => Results.BadRequest(new { message = result.Message }),
            _ => Results.BadRequest(new { message = "Connexion impossible." })
        };
    }
}
