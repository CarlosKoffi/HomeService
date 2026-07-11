using HomeService.Application.Abstractions;
using HomeService.Application.Security;
using HomeService.Contracts.ProviderPortal;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.ProviderPortal;

public sealed class ProviderSelfRegistrationService(IAppDbContext db)
{
    public async Task<ProviderSelfRegistrationResponse> RegisterAsync(
        ProviderSelfRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return new ProviderSelfRegistrationResponse(Guid.Empty, "ValidationFailed", "Le mot de passe doit contenir au moins 8 caracteres.");
        }

        if (request.Password != request.ConfirmPassword)
        {
            return new ProviderSelfRegistrationResponse(Guid.Empty, "ValidationFailed", "Les deux mots de passe ne correspondent pas.");
        }

        var provider = new ProviderProfile(
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.DateOfBirth,
            request.Address,
            ParseProviderGender(request.Gender),
            Math.Max(0, request.YearsOfExperience),
            request.Latitude,
            request.Longitude,
            Math.Clamp(request.MissionRadiusKm, 1, 100));
        provider.SetPortalPassword(Sha256PasswordHasher.Hash(request.Password));

        var requestedServiceIds = request.Services.Select(service => service.ServiceId).Distinct().ToList();
        var activeServiceIds = await db.Services
            .Where(service => requestedServiceIds.Contains(service.Id) && service.IsActive)
            .Select(service => service.Id)
            .ToListAsync(cancellationToken);

        provider.SyncCandidateServices(request.Services
            .Where(service => activeServiceIds.Contains(service.ServiceId))
            .Select(service => (
                service.ServiceId,
                ParseExperienceLevel(service.ExperienceLevel),
                Math.Max(0, service.YearsOfExperience))));

        db.Providers.Add(provider);
        await db.SaveChangesAsync(cancellationToken);

        return new ProviderSelfRegistrationResponse(
            provider.Id,
            provider.Status.ToString(),
            "Profil cree. Candidatez a une entreprise pour devenir interimaire assignable.");
    }

    private static ExperienceLevel ParseExperienceLevel(string? value)
    {
        return Enum.TryParse<ExperienceLevel>(value, true, out var level)
            ? level
            : ExperienceLevel.Confirmed;
    }

    private static ProviderGender ParseProviderGender(string? value)
    {
        return Enum.TryParse<ProviderGender>(value, true, out var gender)
            ? gender
            : ProviderGender.Unspecified;
    }
}
