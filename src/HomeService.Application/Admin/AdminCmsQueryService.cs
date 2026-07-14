using HomeService.Application.Abstractions;
using HomeService.Contracts.Cms;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCmsQueryService(IAppDbContext db)
{
    public async Task<IReadOnlyList<CmsSiteSummaryResponse>> ListSitesAsync(CancellationToken cancellationToken)
    {
        return await db.CmsSites
            .AsNoTracking()
            .OrderBy(site => site.Surface)
            .ThenBy(site => site.Name)
            .Select(site => new CmsSiteSummaryResponse(
                site.Id,
                site.Code,
                site.Name,
                site.Surface.ToString(),
                site.Status.ToString(),
                site.DefaultCountry == null ? null : site.DefaultCountry.IsoCode,
                site.DefaultLanguage!.Code,
                site.HomePageCode,
                site.Pages.Count,
                site.Menus.Count,
                site.CreatedAt,
                site.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CmsSiteDetailResponse?> GetSiteAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.CmsSites
            .AsNoTracking()
            .Where(site => site.Id == id)
            .Select(site => new CmsSiteDetailResponse(
                site.Id,
                site.Code,
                site.Name,
                site.Surface.ToString(),
                site.Status.ToString(),
                site.DefaultCountry == null ? null : site.DefaultCountry.IsoCode,
                site.DefaultLanguage!.Code,
                site.HomePageCode,
                site.Pages
                    .OrderBy(page => page.InternalName)
                    .Select(page => new CmsPageSummaryResponse(
                        page.Id,
                        page.SiteId,
                        page.Code,
                        page.InternalName,
                        page.TemplateKey,
                        page.Status.ToString(),
                        page.RequiresAuthentication,
                        page.Translations
                            .Where(translation => translation.LanguageId == site.DefaultLanguageId)
                            .Select(translation => translation.Title)
                            .FirstOrDefault(),
                        page.Translations
                            .Where(translation => translation.LanguageId == site.DefaultLanguageId)
                            .Select(translation => translation.Slug)
                            .FirstOrDefault(),
                        page.Versions.Count,
                        page.Versions.SelectMany(version => version.Sections).Count(),
                        page.CreatedAt,
                        page.UpdatedAt))
                    .ToList(),
                site.Menus
                    .OrderBy(menu => menu.Placement)
                    .ThenBy(menu => menu.Name)
                    .Select(menu => new CmsMenuSummaryResponse(
                        menu.Id,
                        menu.Code,
                        menu.Name,
                        menu.Placement,
                        menu.IsActive,
                        menu.Items.Count))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CmsPageSummaryResponse>> ListPagesAsync(Guid siteId, CancellationToken cancellationToken)
    {
        var siteLanguageId = await db.CmsSites
            .AsNoTracking()
            .Where(site => site.Id == siteId)
            .Select(site => (Guid?)site.DefaultLanguageId)
            .FirstOrDefaultAsync(cancellationToken);

        if (siteLanguageId is null)
        {
            return [];
        }

        var defaultLanguageId = siteLanguageId.Value;
        return await db.CmsPages
            .AsNoTracking()
            .Where(page => page.SiteId == siteId)
            .OrderBy(page => page.InternalName)
            .Select(page => new CmsPageSummaryResponse(
                page.Id,
                page.SiteId,
                page.Code,
                page.InternalName,
                page.TemplateKey,
                page.Status.ToString(),
                page.RequiresAuthentication,
                page.Translations
                    .Where(translation => translation.LanguageId == defaultLanguageId)
                    .Select(translation => translation.Title)
                    .FirstOrDefault(),
                page.Translations
                    .Where(translation => translation.LanguageId == defaultLanguageId)
                    .Select(translation => translation.Slug)
                    .FirstOrDefault(),
                page.Versions.Count,
                page.Versions.SelectMany(version => version.Sections).Count(),
                page.CreatedAt,
                page.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CmsComponentDefinitionResponse>> ListComponentDefinitionsAsync(CancellationToken cancellationToken)
    {
        return await db.CmsComponentDefinitions
            .AsNoTracking()
            .OrderBy(component => component.Key)
            .ThenByDescending(component => component.SchemaVersion)
            .Select(component => new CmsComponentDefinitionResponse(
                component.Id,
                component.Key,
                component.Name,
                component.Description,
                component.SchemaVersion,
                component.IsActive))
            .ToListAsync(cancellationToken);
    }
}
