using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Application.Branding;
using HomeService.Application.Companies;
using HomeService.Contracts.Admin;
using HomeService.Contracts.Branding;
using HomeService.Contracts.Companies;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminConfigurationService(IAppDbContext db)
{
    public async Task<AdminCountryBrandingUpdateResult> UpdateCountryBrandingAsync(
        string countryCode,
        UpdateCountryBrandingRequest request,
        CancellationToken cancellationToken)
        => await UpdateCountryBrandingAsync(countryCode, request, null, null, cancellationToken);

    public async Task<AdminCountryBrandingUpdateResult> UpdateCountryBrandingAsync(
        string countryCode,
        UpdateCountryBrandingRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var validationError = CountryBrandingValidator.Validate(request);
        if (validationError is not null)
        {
            return AdminCountryBrandingUpdateResult.ValidationFailed(validationError);
        }

        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();
        var country = await db.Countries.FirstOrDefaultAsync(country => country.IsoCode == normalizedCountryCode, cancellationToken);
        if (country is null)
        {
            return AdminCountryBrandingUpdateResult.CountryNotFound();
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

        var response = new CountryBrandingResponse(
            country.IsoCode,
            country.Name,
            branding.BrandName,
            branding.PrimaryColor,
            branding.SecondaryColor,
            branding.AccentColor,
            branding.HeroTitle,
            branding.HeroSubtitle,
            branding.HeroImageUrl,
            branding.MotifStyle);

        AddAuditLog(
            actor,
            auditContext,
            "AdminCountryBrandingUpdated",
            nameof(CountryBranding),
            branding.Id,
            $"Branding pays {country.IsoCode} mis a jour.",
            before,
            request);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCountryBrandingUpdateResult.Ok(branding, before, request, response);
    }

    public async Task<AdminCompanyAssignmentModeUpdateResult> UpdateCompanyAssignmentModeAsync(
        Guid companyId,
        UpdateCompanyAssignmentModeRequest request,
        CancellationToken cancellationToken)
        => await UpdateCompanyAssignmentModeAsync(companyId, request, null, null, cancellationToken);

    public async Task<AdminCompanyAssignmentModeUpdateResult> UpdateCompanyAssignmentModeAsync(
        Guid companyId,
        UpdateCompanyAssignmentModeRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);
        if (company is null)
        {
            return AdminCompanyAssignmentModeUpdateResult.NotFound();
        }

        if (!TryParseCompanyAssignmentMode(request.AssignmentMode, out var assignmentMode))
        {
            return AdminCompanyAssignmentModeUpdateResult.ValidationFailed("Mode d'affectation invalide.");
        }

        var previousMode = company.AssignmentMode;
        company.ChangeAssignmentMode(assignmentMode);
        var before = new { AssignmentMode = previousMode };
        var after = new { company.AssignmentMode };
        AddAuditLog(
            actor,
            auditContext,
            "AdminCompanyAssignmentModeUpdated",
            nameof(Company),
            company.Id,
            "Mode d'affectation entreprise modifie.",
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyAssignmentModeUpdateResult.Ok(
            company,
            before,
            after,
            CompanyAssignmentModePresenter.ToResponse(company));
    }

    public async Task<AdminCompanyDispatchSettingsUpdateResult> UpdateCompanyDispatchSettingsAsync(
        Guid companyId,
        UpdateAdminCompanyDispatchSettingsRequest request,
        CancellationToken cancellationToken)
        => await UpdateCompanyDispatchSettingsAsync(companyId, request, null, null, cancellationToken);

    public async Task<AdminCompanyDispatchSettingsUpdateResult> UpdateCompanyDispatchSettingsAsync(
        Guid companyId,
        UpdateAdminCompanyDispatchSettingsRequest request,
        AuditActor? actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);
        if (company is null)
        {
            return AdminCompanyDispatchSettingsUpdateResult.NotFound();
        }

        if (request.MissionDispatchPriority is < 0 or > 9999)
        {
            return AdminCompanyDispatchSettingsUpdateResult.ValidationFailed("La priorite mission doit etre comprise entre 0 et 9999.");
        }

        var before = new
        {
            company.MissionDispatchPriority,
            company.AcceptsUrgentMissions
        };

        company.UpdateMissionDispatchSettings(request.MissionDispatchPriority, request.AcceptsUrgentMissions);

        var after = new
        {
            company.MissionDispatchPriority,
            company.AcceptsUrgentMissions
        };
        AddAuditLog(
            actor,
            auditContext,
            "AdminCompanyDispatchSettingsUpdated",
            nameof(Company),
            company.Id,
            "Parametres de reception des missions modifies.",
            before,
            after);
        await db.SaveChangesAsync(cancellationToken);

        return AdminCompanyDispatchSettingsUpdateResult.Ok(company, before, after);
    }

    private static bool TryParseCompanyAssignmentMode(string? value, out CompanyAssignmentMode assignmentMode)
    {
        return Enum.TryParse(value?.Trim(), true, out assignmentMode);
    }

    private void AddAuditLog(
        AuditActor? actor,
        AuditRequestContext? auditContext,
        string action,
        string entityType,
        Guid? entityId,
        string summary,
        object? before = null,
        object? after = null)
    {
        if (actor is null)
        {
            return;
        }

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            action,
            entityType,
            entityId,
            summary,
            auditContext,
            before,
            after));
    }
}

public enum AdminConfigurationUpdateStatus
{
    Ok,
    NotFound,
    ValidationFailed
}

public sealed record AdminCountryBrandingUpdateResult(
    AdminConfigurationUpdateStatus Status,
    CountryBranding? Branding,
    object? Before,
    object? After,
    CountryBrandingResponse? Response,
    string? Message)
{
    public static AdminCountryBrandingUpdateResult Ok(CountryBranding branding, object? before, object? after, CountryBrandingResponse response)
        => new(AdminConfigurationUpdateStatus.Ok, branding, before, after, response, null);

    public static AdminCountryBrandingUpdateResult CountryNotFound()
        => new(AdminConfigurationUpdateStatus.NotFound, null, null, null, null, "Pays introuvable.");

    public static AdminCountryBrandingUpdateResult ValidationFailed(string message)
        => new(AdminConfigurationUpdateStatus.ValidationFailed, null, null, null, null, message);
}

public sealed record AdminCompanyAssignmentModeUpdateResult(
    AdminConfigurationUpdateStatus Status,
    Company? Company,
    object? Before,
    object? After,
    CompanyAssignmentModeResponse? Response,
    string? Message)
{
    public static AdminCompanyAssignmentModeUpdateResult Ok(Company company, object? before, object? after, CompanyAssignmentModeResponse response)
        => new(AdminConfigurationUpdateStatus.Ok, company, before, after, response, null);

    public static AdminCompanyAssignmentModeUpdateResult NotFound()
        => new(AdminConfigurationUpdateStatus.NotFound, null, null, null, null, "Entreprise introuvable.");

    public static AdminCompanyAssignmentModeUpdateResult ValidationFailed(string message)
        => new(AdminConfigurationUpdateStatus.ValidationFailed, null, null, null, null, message);
}

public sealed record AdminCompanyDispatchSettingsUpdateResult(
    AdminConfigurationUpdateStatus Status,
    Company? Company,
    object? Before,
    object? After,
    string? Message)
{
    public static AdminCompanyDispatchSettingsUpdateResult Ok(Company company, object? before, object? after)
        => new(AdminConfigurationUpdateStatus.Ok, company, before, after, null);

    public static AdminCompanyDispatchSettingsUpdateResult NotFound()
        => new(AdminConfigurationUpdateStatus.NotFound, null, null, null, "Entreprise introuvable.");

    public static AdminCompanyDispatchSettingsUpdateResult ValidationFailed(string message)
        => new(AdminConfigurationUpdateStatus.ValidationFailed, null, null, null, message);
}
