using System.Globalization;
using System.Text;
using HomeService.Domain.Entities;

namespace HomeService.Application.CompanyPortal;

public sealed record AbidjanInterventionZone(
    string Code,
    string Commune,
    string Name,
    decimal Latitude,
    decimal Longitude);

public sealed class EmployeeInterventionZoneService
{
    public const int DefaultRadiusKm = 8;
    public const string Explanation =
        "Les zones proposées sont calculées autour de l’adresse de l’entreprise et de celle de cet employé (rayon de 8 km). Vous pouvez les modifier.";

    public IReadOnlyList<AbidjanInterventionZone> Zones { get; } =
    [
        Zone("abobo-abobo-baoule", "Abobo", "Abobo Baoulé", 5.4209m, -4.0057m),
        Zone("abobo-avocatier", "Abobo", "Avocatier", 5.4303m, -4.0207m),
        Zone("abobo-dokui", "Abobo", "Dokui", 5.3970m, -4.0035m),
        Zone("abobo-belleville", "Abobo", "Belleville", 5.4290m, -4.0340m),
        Zone("abobo-sagbe", "Abobo", "Sagbé", 5.4420m, -4.0180m),
        Zone("abobo-pk18", "Abobo", "PK18", 5.4940m, -4.0530m),
        Zone("abobo-anonkoua-koute", "Abobo", "Anonkoua Kouté", 5.4610m, -4.0530m),

        Zone("adjame-220-logements", "Adjamé", "220 Logements", 5.3687m, -4.0204m),
        Zone("adjame-liberte", "Adjamé", "Liberté", 5.3650m, -4.0280m),
        Zone("adjame-williamsville", "Adjamé", "Williamsville", 5.3813m, -4.0175m),
        Zone("adjame-mairie", "Adjamé", "Mairie", 5.3608m, -4.0230m),
        Zone("adjame-habitat", "Adjamé", "Habitat", 5.3750m, -4.0285m),

        Zone("attecoube-locodjro", "Attécoubé", "Locodjro", 5.3420m, -4.0800m),
        Zone("attecoube-abobodoume", "Attécoubé", "Abobodoumé", 5.3340m, -4.0670m),
        Zone("attecoube-sante", "Attécoubé", "Santé", 5.3500m, -4.0490m),
        Zone("attecoube-mossikro", "Attécoubé", "Mossikro", 5.3560m, -4.0600m),

        Zone("bingerville-centre", "Bingerville", "Centre", 5.3550m, -3.8850m),
        Zone("bingerville-feh-kesse", "Bingerville", "Feh Kessé", 5.3730m, -3.9070m),
        Zone("bingerville-akandje", "Bingerville", "Akandjé", 5.4010m, -3.8840m),

        Zone("cocody-angre", "Cocody", "Angré", 5.3900m, -3.9880m),
        Zone("cocody-deux-plateaux", "Cocody", "Deux-Plateaux", 5.3710m, -3.9870m),
        Zone("cocody-riviera-1", "Cocody", "Riviera 1", 5.3490m, -3.9770m),
        Zone("cocody-riviera-2", "Cocody", "Riviera 2", 5.3560m, -3.9600m),
        Zone("cocody-riviera-3", "Cocody", "Riviera 3", 5.3600m, -3.9450m),
        Zone("cocody-riviera-4", "Cocody", "Riviera 4", 5.3440m, -3.9320m),
        Zone("cocody-bonoumin", "Cocody", "Bonoumin", 5.3750m, -3.9590m),
        Zone("cocody-mpouto", "Cocody", "M’Pouto", 5.3390m, -3.9290m),
        Zone("cocody-danga", "Cocody", "Danga", 5.3370m, -4.0020m),
        Zone("cocody-blockhauss", "Cocody", "Blockhauss", 5.3260m, -4.0060m),
        Zone("cocody-palmeraie", "Cocody", "Palmeraie", 5.3790m, -3.9460m),
        Zone("cocody-akouedo", "Cocody", "Akouédo", 5.3810m, -3.9210m),

        Zone("koumassi-remblais", "Koumassi", "Remblais", 5.2860m, -3.9550m),
        Zone("koumassi-campement", "Koumassi", "Campement", 5.3000m, -3.9480m),
        Zone("koumassi-divo", "Koumassi", "Divo", 5.2940m, -3.9660m),
        Zone("koumassi-prodomo", "Koumassi", "Prodomo", 5.3020m, -3.9560m),
        Zone("koumassi-sicogi", "Koumassi", "Sicogi", 5.2990m, -3.9720m),

        Zone("marcory-zone-4", "Marcory", "Zone 4", 5.2950m, -3.9870m),
        Zone("marcory-bietry", "Marcory", "Biétry", 5.2900m, -3.9780m),
        Zone("marcory-residentiel", "Marcory", "Résidentiel", 5.3030m, -3.9900m),
        Zone("marcory-anoumabo", "Marcory", "Anoumabo", 5.3100m, -3.9730m),
        Zone("marcory-sans-fil", "Marcory", "Sans Fil", 5.3090m, -3.9990m),

        Zone("plateau-centre", "Plateau", "Centre", 5.3230m, -4.0200m),
        Zone("plateau-indenie", "Plateau", "Indénié", 5.3410m, -4.0150m),

        Zone("port-bouet-vridi", "Port-Bouët", "Vridi", 5.2580m, -4.0060m),
        Zone("port-bouet-zone-aeroportuaire", "Port-Bouët", "Zone aéroportuaire", 5.2600m, -3.9300m),
        Zone("port-bouet-gonzagueville", "Port-Bouët", "Gonzagueville", 5.2510m, -3.9000m),
        Zone("port-bouet-jean-folly", "Port-Bouët", "Jean-Folly", 5.2450m, -3.9250m),
        Zone("port-bouet-adjouffou", "Port-Bouët", "Adjouffou", 5.2810m, -3.9190m),

        Zone("treichville-belleville", "Treichville", "Belleville", 5.2970m, -4.0100m),
        Zone("treichville-arras", "Treichville", "Arras", 5.3010m, -4.0150m),
        Zone("treichville-avenue-8", "Treichville", "Avenue 8", 5.2930m, -4.0050m),

        Zone("yopougon-niangon", "Yopougon", "Niangon", 5.3330m, -4.1100m),
        Zone("yopougon-selmer", "Yopougon", "Selmer", 5.3400m, -4.0850m),
        Zone("yopougon-sicogi", "Yopougon", "Sicogi", 5.3350m, -4.0750m),
        Zone("yopougon-maroc", "Yopougon", "Maroc", 5.3560m, -4.1000m),
        Zone("yopougon-wassakara", "Yopougon", "Wassakara", 5.3480m, -4.0760m),
        Zone("yopougon-toits-rouges", "Yopougon", "Toits-Rouges", 5.3410m, -4.0640m),
        Zone("yopougon-gesco", "Yopougon", "Gesco", 5.3890m, -4.1070m),
        Zone("yopougon-sideci", "Yopougon", "Sideci", 5.3470m, -4.0880m),
        Zone("yopougon-koute", "Yopougon", "Kouté", 5.3730m, -4.0730m)
    ];

    public IReadOnlyList<string> BuildSuggestedZoneCodes(Company company, ProviderProfile provider, int radiusKm = DefaultRadiusKm)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAroundCoordinates(selected, company.Latitude, company.Longitude, radiusKm);
        AddAroundCoordinates(selected, provider.MissionLatitude, provider.MissionLongitude, radiusKm);

        // Historical records can predate Google Places. Commune matching provides a safe
        // one-time catch-up until the addresses are selected again from Places.
        if (company.Latitude is null || company.Longitude is null)
        {
            AddByAddress(selected, $"{company.Address} {company.City}");
        }

        if (provider.MissionLatitude is null || provider.MissionLongitude is null)
        {
            AddByAddress(selected, provider.Address);
        }

        return selected.OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void ApplySuggestion(Company company, ProviderProfile provider, int radiusKm = DefaultRadiusKm)
    {
        provider.ApplySuggestedInterventionZones(BuildSuggestedZoneCodes(company, provider, radiusKm), radiusKm);
    }

    private void AddAroundCoordinates(HashSet<string> selected, decimal? latitude, decimal? longitude, int radiusKm)
    {
        if (latitude is null || longitude is null)
        {
            return;
        }

        foreach (var zone in Zones.Where(zone => DistanceKm(latitude.Value, longitude.Value, zone.Latitude, zone.Longitude) <= radiusKm))
        {
            selected.Add(zone.Code);
        }
    }

    private void AddByAddress(HashSet<string> selected, string? address)
    {
        var normalizedAddress = Normalize(address);
        if (normalizedAddress.Length == 0)
        {
            return;
        }

        foreach (var commune in Zones.Select(zone => zone.Commune).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!normalizedAddress.Contains(Normalize(commune), StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var zone in Zones.Where(zone => zone.Commune.Equals(commune, StringComparison.OrdinalIgnoreCase)))
            {
                selected.Add(zone.Code);
            }
        }
    }

    private static double DistanceKm(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        const double earthRadiusKm = 6371d;
        var lat1 = DegreesToRadians((double)latitude1);
        var lat2 = DegreesToRadians((double)latitude2);
        var deltaLatitude = DegreesToRadians((double)(latitude2 - latitude1));
        var deltaLongitude = DegreesToRadians((double)(longitude2 - longitude1));
        var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static AbidjanInterventionZone Zone(string code, string commune, string name, decimal latitude, decimal longitude) =>
        new(code, commune, name, latitude, longitude);
}
