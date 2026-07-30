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
        return new ClientMeResponse(customer.Id, customer.FirstName, customer.LastName, customer.PhoneNumber, customer.Email);
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
                method.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientPaymentMethodResult> AddPaymentMethodAsync(Guid customerId, UpsertClientPaymentMethodRequest request, CancellationToken cancellationToken)
    {
        if (!TryParsePaymentMethod(request.Method, out var method))
        {
            return ClientPaymentMethodResult.Invalid("Mode de paiement invalide.");
        }

        if (request.IsDefault)
        {
            await ClearDefaultPaymentMethodsAsync(customerId, cancellationToken);
        }

        var paymentMethod = new CustomerPaymentMethod(customerId, method, request.Label, request.MaskedReference, request.IsDefault);
        db.CustomerPaymentMethods.Add(paymentMethod);
        await db.SaveChangesAsync(cancellationToken);
        return ClientPaymentMethodResult.Ok(ToPaymentMethodResponse(paymentMethod));
    }

    public async Task<bool> DeletePaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken cancellationToken)
    {
        var paymentMethod = await db.CustomerPaymentMethods.FirstOrDefaultAsync(item => item.Id == paymentMethodId && item.CustomerId == customerId, cancellationToken);
        if (paymentMethod is null)
        {
            return false;
        }

        paymentMethod.Disable();
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
            paymentMethod.IsActive);
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
