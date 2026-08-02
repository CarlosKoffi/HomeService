using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminPaymentProviderService(IAppDbContext db)
{
    public async Task<IReadOnlyList<PaymentProviderResponse>> ListAsync(CancellationToken cancellationToken) =>
        await db.PaymentProviders.AsNoTracking()
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => ToResponse(item))
            .ToListAsync(cancellationToken);

    public async Task<PaymentProviderResponse> CreateAsync(UpsertPaymentProviderRequest request, CancellationToken cancellationToken)
    {
        var method = ParseMethod(request.Method);
        var code = NormalizeCode(request.Code);
        if (await db.PaymentProviders.AnyAsync(item => item.Code == code, cancellationToken))
            throw new InvalidOperationException("Un operateur utilise deja ce code.");

        var provider = new PaymentProvider(code, request.Name, method, request.Description, request.LogoUrl, request.SortOrder);
        provider.SetActive(request.IsActive);
        db.PaymentProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(provider);
    }

    public async Task<PaymentProviderResponse?> UpdateAsync(Guid id, UpsertPaymentProviderRequest request, CancellationToken cancellationToken)
    {
        var provider = await db.PaymentProviders.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (provider is null) return null;

        var code = NormalizeCode(request.Code);
        if (await db.PaymentProviders.AnyAsync(item => item.Id != id && item.Code == code, cancellationToken))
            throw new InvalidOperationException("Un autre operateur utilise deja ce code.");

        provider.Update(code, request.Name, ParseMethod(request.Method), request.Description, request.LogoUrl, request.SortOrder);
        provider.SetActive(request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(provider);
    }

    private static PaymentMethod ParseMethod(string value) =>
        Enum.TryParse<PaymentMethod>(value, true, out var method) && method is PaymentMethod.MobileMoney or PaymentMethod.Card
            ? method
            : throw new ArgumentException("Le type doit etre MobileMoney ou Card.");

    private static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Le code est obligatoire.");
        return value.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static PaymentProviderResponse ToResponse(PaymentProvider item) =>
        new(item.Id, item.Code, item.Name, item.Method.ToString(), item.Description, item.LogoUrl, item.IsActive, item.SortOrder);
}
