using HomeService.Application.Abstractions;
using HomeService.Contracts.Clients;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Clients;

public sealed class ClientProfileService(IAppDbContext db)
{
    public ClientMeResponse ToMe(CustomerProfile customer)
    {
        return new ClientMeResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.PhoneNumber,
            customer.Email,
            customer.ProfilePhotoPath is null ? null : "/api/client/me/photo");
    }

    public async Task<ClientProfilePhotoResponse> UpdatePhotoAsync(
        Guid customerId,
        string storagePath,
        CancellationToken cancellationToken)
    {
        var updated = await db.Customers
            .Where(customer => customer.Id == customerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(customer => customer.ProfilePhotoPath, storagePath),
                cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException("Le profil client n'existe plus. Reconnectez-vous puis reessayez.");
        }

        return new ClientProfilePhotoResponse("/api/client/me/photo");
    }

    public async Task<ClientMeResponse> UpdateAsync(CustomerProfile customer, UpdateClientProfileRequest request, CancellationToken cancellationToken)
    {
        customer.UpdateProfile(request.FirstName, request.LastName, request.Email);
        await db.SaveChangesAsync(cancellationToken);
        return ToMe(customer);
    }

    public async Task<IReadOnlyList<ClientAddressResponse>> ListAddressesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return await db.CustomerAddresses
            .AsNoTracking()
            .Where(address => address.CustomerId == customerId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Label)
            .Select(address => new ClientAddressResponse(
                address.Id,
                address.Label,
                address.AddressLine,
                address.Latitude,
                address.Longitude,
                address.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientAddressResponse> AddAddressAsync(Guid customerId, UpsertClientAddressRequest request, CancellationToken cancellationToken)
    {
        if (request.IsDefault)
        {
            await ClearDefaultAddressesAsync(customerId, cancellationToken);
        }

        var address = new CustomerAddress(customerId, request.Label, request.AddressLine, request.Latitude, request.Longitude, request.IsDefault);
        db.CustomerAddresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        return ToAddressResponse(address);
    }

    public async Task<ClientAddressResult> UpdateAddressAsync(Guid customerId, Guid addressId, UpsertClientAddressRequest request, CancellationToken cancellationToken)
    {
        var address = await db.CustomerAddresses.FirstOrDefaultAsync(item => item.Id == addressId && item.CustomerId == customerId, cancellationToken);
        if (address is null)
        {
            return ClientAddressResult.NotFound();
        }

        if (request.IsDefault)
        {
            await ClearDefaultAddressesAsync(customerId, cancellationToken);
        }

        address.Update(request.Label, request.AddressLine, request.Latitude, request.Longitude, request.IsDefault);
        await db.SaveChangesAsync(cancellationToken);
        return ClientAddressResult.Ok(ToAddressResponse(address));
    }

    public async Task<bool> DeleteAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await db.CustomerAddresses.FirstOrDefaultAsync(item => item.Id == addressId && item.CustomerId == customerId, cancellationToken);
        if (address is null)
        {
            return false;
        }

        db.CustomerAddresses.Remove(address);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<ClientPaymentMethodResponse>> ListPaymentMethodsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return await db.CustomerPaymentMethods
            .AsNoTracking()
            .Where(method => method.CustomerId == customerId && method.IsActive)
            .OrderByDescending(method => method.IsDefault)
            .ThenBy(method => method.Label)
            .Select(method => new ClientPaymentMethodResponse(
                method.Id,
                method.Method.ToString(),
                method.Label,
                method.MaskedReference,
                method.IsDefault,
                method.IsActive,
                method.PaymentProviderId,
                method.PaymentProvider != null ? method.PaymentProvider.Name : null,
                method.PaymentProvider != null ? method.PaymentProvider.LogoUrl : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentProviderResponse>> ListPaymentProvidersAsync(CancellationToken cancellationToken)
        => await db.PaymentProviders.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new PaymentProviderResponse(item.Id, item.Code, item.Name, item.Method.ToString(), item.Description, item.LogoUrl, item.IsActive, item.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<ClientPaymentMethodResult> AddPaymentMethodAsync(Guid customerId, UpsertClientPaymentMethodRequest request, CancellationToken cancellationToken)
    {
        if (!TryParsePaymentMethod(request.Method, out var method))
        {
            return ClientPaymentMethodResult.Invalid("Mode de paiement invalide.");
        }

        PaymentProvider? provider = null;
        if (request.PaymentProviderId.HasValue)
        {
            provider = await db.PaymentProviders.FirstOrDefaultAsync(
                item => item.Id == request.PaymentProviderId.Value && item.IsActive,
                cancellationToken);
            if (provider is null || provider.Method != method)
            {
                return ClientPaymentMethodResult.Invalid("Le fournisseur de paiement selectionne est invalide.");
            }
        }

        if (request.IsDefault)
        {
            await ClearDefaultPaymentMethodsAsync(customerId, cancellationToken);
        }

        var paymentMethod = new CustomerPaymentMethod(customerId, provider?.Id, method, provider?.Name ?? request.Label, request.MaskedReference, request.IsDefault);
        db.CustomerPaymentMethods.Add(paymentMethod);
        await db.SaveChangesAsync(cancellationToken);
        return ClientPaymentMethodResult.Ok(new ClientPaymentMethodResponse(
            paymentMethod.Id,
            paymentMethod.Method.ToString(),
            paymentMethod.Label,
            paymentMethod.MaskedReference,
            paymentMethod.IsDefault,
            paymentMethod.IsActive,
            provider?.Id,
            provider?.Name,
            provider?.LogoUrl));
    }

    public async Task<ClientMobileMoneyAccountResult> AddMobileMoneyAccountAsync(
        Guid customerId,
        CreateClientMobileMoneyAccountRequest request,
        CancellationToken cancellationToken)
    {
        var digits = new string((request.PhoneNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length is < 8 or > 15)
        {
            return ClientMobileMoneyAccountResult.Invalid("Saisissez un numero Mobile Money valide.");
        }

        var providerIds = request.PaymentProviderIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];
        if (providerIds.Count == 0)
        {
            return ClientMobileMoneyAccountResult.Invalid("Choisissez au moins un reseau Mobile Money.");
        }

        var providers = await db.PaymentProviders
            .Where(provider => providerIds.Contains(provider.Id)
                && provider.IsActive
                && provider.Method == PaymentMethod.MobileMoney)
            .OrderBy(provider => provider.SortOrder)
            .ThenBy(provider => provider.Name)
            .ToListAsync(cancellationToken);
        if (providers.Count != providerIds.Count)
        {
            return ClientMobileMoneyAccountResult.Invalid("Un des reseaux Mobile Money selectionnes est invalide.");
        }

        if (request.IsDefault)
        {
            await ClearDefaultPaymentMethodsAsync(customerId, cancellationToken);
        }

        var maskedReference = $"**** {digits[^4..]}";
        var methods = new List<CustomerPaymentMethod>(providers.Count);
        for (var index = 0; index < providers.Count; index++)
        {
            var provider = providers[index];
            var method = new CustomerPaymentMethod(
                customerId,
                provider.Id,
                PaymentMethod.MobileMoney,
                provider.Name,
                maskedReference,
                request.IsDefault && index == 0);
            db.CustomerPaymentMethods.Add(method);
            methods.Add(method);
        }

        await db.SaveChangesAsync(cancellationToken);
        var responses = methods
            .Select((method, index) => new ClientPaymentMethodResponse(
                method.Id,
                method.Method.ToString(),
                method.Label,
                method.MaskedReference,
                method.IsDefault,
                method.IsActive,
                providers[index].Id,
                providers[index].Name,
                providers[index].LogoUrl))
            .ToList();

        return ClientMobileMoneyAccountResult.Ok(new CreateClientMobileMoneyAccountResponse(maskedReference, responses));
    }

    public async Task<ClientMobileMoneyAccountResult> UpdateMobileMoneyAccountAsync(
        Guid customerId,
        Guid paymentMethodId,
        UpdateClientMobileMoneyAccountRequest request,
        CancellationToken cancellationToken)
    {
        var target = await db.CustomerPaymentMethods
            .FirstOrDefaultAsync(method => method.Id == paymentMethodId
                && method.CustomerId == customerId
                && method.Method == PaymentMethod.MobileMoney
                && method.IsActive,
                cancellationToken);
        if (target is null || string.IsNullOrWhiteSpace(target.MaskedReference))
        {
            return ClientMobileMoneyAccountResult.Invalid("Ce numero Mobile Money est introuvable.");
        }

        var providerIds = request.PaymentProviderIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? [];
        if (providerIds.Count == 0)
        {
            return ClientMobileMoneyAccountResult.Invalid("Conservez au moins un reseau Mobile Money.");
        }

        var providers = await db.PaymentProviders
            .Where(provider => providerIds.Contains(provider.Id)
                && provider.IsActive
                && provider.Method == PaymentMethod.MobileMoney)
            .OrderBy(provider => provider.SortOrder)
            .ThenBy(provider => provider.Name)
            .ToListAsync(cancellationToken);
        if (providers.Count != providerIds.Count)
        {
            return ClientMobileMoneyAccountResult.Invalid("Un des reseaux Mobile Money selectionnes est invalide.");
        }

        var accountMethods = await db.CustomerPaymentMethods
            .Where(method => method.CustomerId == customerId
                && method.Method == PaymentMethod.MobileMoney
                && method.MaskedReference == target.MaskedReference)
            .ToListAsync(cancellationToken);
        var accountWasDefault = accountMethods.Any(method => method.IsActive && method.IsDefault);
        foreach (var method in accountMethods)
        {
            method.SetDefault(false);
            if (method.IsActive && (!method.PaymentProviderId.HasValue || !providerIds.Contains(method.PaymentProviderId.Value)))
            {
                method.Disable();
            }
        }

        var selectedMethods = new List<CustomerPaymentMethod>(providers.Count);
        foreach (var provider in providers)
        {
            var method = accountMethods.FirstOrDefault(item => item.PaymentProviderId == provider.Id);
            if (method is null)
            {
                method = new CustomerPaymentMethod(
                    customerId,
                    provider.Id,
                    PaymentMethod.MobileMoney,
                    provider.Name,
                    target.MaskedReference,
                    isDefault: false);
                db.CustomerPaymentMethods.Add(method);
            }
            else
            {
                method.Update(PaymentMethod.MobileMoney, provider.Name, target.MaskedReference, isDefault: false);
            }

            selectedMethods.Add(method);
        }

        if (accountWasDefault)
        {
            selectedMethods[0].SetDefault(true);
        }

        await db.SaveChangesAsync(cancellationToken);
        var responses = selectedMethods
            .Select((method, index) => new ClientPaymentMethodResponse(
                method.Id,
                method.Method.ToString(),
                method.Label,
                method.MaskedReference,
                method.IsDefault,
                method.IsActive,
                providers[index].Id,
                providers[index].Name,
                providers[index].LogoUrl))
            .ToList();

        return ClientMobileMoneyAccountResult.Ok(new CreateClientMobileMoneyAccountResponse(target.MaskedReference, responses));
    }

    public async Task<bool> DeletePaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken cancellationToken)
    {
        var paymentMethod = await db.CustomerPaymentMethods.FirstOrDefaultAsync(
            item => item.Id == paymentMethodId && item.CustomerId == customerId && item.IsActive,
            cancellationToken);
        if (paymentMethod is null)
        {
            return false;
        }

        var wasDefault = paymentMethod.IsDefault;
        paymentMethod.Disable();
        paymentMethod.SetDefault(false);

        if (wasDefault)
        {
            var replacement = await db.CustomerPaymentMethods
                .Where(item => item.CustomerId == customerId && item.IsActive && item.Id != paymentMethodId)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            replacement?.SetDefault(true);
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ClearDefaultAddressesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var addresses = await db.CustomerAddresses
            .Where(address => address.CustomerId == customerId && address.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var address in addresses)
        {
            address.SetDefault(false);
        }
    }

    private async Task ClearDefaultPaymentMethodsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var methods = await db.CustomerPaymentMethods
            .Where(method => method.CustomerId == customerId && method.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var method in methods)
        {
            method.SetDefault(false);
        }
    }

    private static bool TryParsePaymentMethod(string value, out PaymentMethod method)
    {
        return Enum.TryParse(value, true, out method)
            && method is PaymentMethod.MobileMoney or PaymentMethod.Card;
    }

    private static ClientAddressResponse ToAddressResponse(CustomerAddress address)
    {
        return new ClientAddressResponse(address.Id, address.Label, address.AddressLine, address.Latitude, address.Longitude, address.IsDefault);
    }

    private static ClientPaymentMethodResponse ToPaymentMethodResponse(CustomerPaymentMethod paymentMethod)
    {
        return new ClientPaymentMethodResponse(
            paymentMethod.Id,
            paymentMethod.Method.ToString(),
            paymentMethod.Label,
            paymentMethod.MaskedReference,
            paymentMethod.IsDefault,
            paymentMethod.IsActive,
            paymentMethod.PaymentProviderId,
            paymentMethod.PaymentProvider?.Name,
            paymentMethod.PaymentProvider?.LogoUrl);
    }
}

public sealed record ClientAddressResult(bool IsSuccess, ClientAddressResponse? Response)
{
    public static ClientAddressResult Ok(ClientAddressResponse response) => new(true, response);
    public static ClientAddressResult NotFound() => new(false, null);
}

public sealed record ClientPaymentMethodResult(bool IsSuccess, ClientPaymentMethodResponse? Response, string? Message)
{
    public static ClientPaymentMethodResult Ok(ClientPaymentMethodResponse response) => new(true, response, null);
    public static ClientPaymentMethodResult Invalid(string message) => new(false, null, message);
}

public sealed record ClientMobileMoneyAccountResult(
    bool IsSuccess,
    CreateClientMobileMoneyAccountResponse? Response,
    string? Message)
{
    public static ClientMobileMoneyAccountResult Ok(CreateClientMobileMoneyAccountResponse response) => new(true, response, null);
    public static ClientMobileMoneyAccountResult Invalid(string message) => new(false, null, message);
}
