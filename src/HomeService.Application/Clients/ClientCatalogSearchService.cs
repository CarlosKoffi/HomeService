using System.Globalization;
using System.Text;
using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientCatalogSearchService(IAppDbContext db)
{
    public async Task<IReadOnlyList<ClientCatalogSearchResultResponse>> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        var services = await db.Services
            .AsNoTracking()
            .Include(service => service.Prestations)
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .ToListAsync(cancellationToken);

        var normalizedQuery = Normalize(query);
        var results = new List<ClientCatalogSearchResultResponse>();

        foreach (var service in services)
        {
            var serviceMatches = string.IsNullOrWhiteSpace(normalizedQuery)
                || Normalize(service.Name).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || Normalize(service.Description).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);

            if (serviceMatches)
            {
                results.Add(new ClientCatalogSearchResultResponse(
                    service.Id,
                    "Service",
                    service.Name,
                    service.Description,
                    service.Id,
                    service.Name,
                    null,
                    null,
                    service.PriceMinAmount,
                    service.PriceMaxAmount,
                    service.Currency,
                    service.IconName));
            }

            foreach (var prestation in service.Prestations.Where(prestation => prestation.IsActive))
            {
                var prestationMatches = string.IsNullOrWhiteSpace(normalizedQuery)
                    || Normalize(prestation.Name).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || Normalize(prestation.Description).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || serviceMatches;

                if (!prestationMatches)
                {
                    continue;
                }

                results.Add(new ClientCatalogSearchResultResponse(
                    prestation.Id,
                    "Prestation",
                    $"{service.Name} - {prestation.Name}",
                    prestation.Description,
                    service.Id,
                    service.Name,
                    prestation.Id,
                    prestation.Name,
                    prestation.PriceMinAmount,
                    prestation.PriceMaxAmount,
                    prestation.Currency,
                    service.IconName));
            }
        }

        return results
            .Take(40)
            .ToList();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
