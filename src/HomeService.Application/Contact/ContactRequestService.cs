using HomeService.Application.Abstractions;
using HomeService.Contracts.Contact;
using HomeService.Domain.Entities;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Contact;

public sealed class ContactRequestService(IAppDbContext db)
{
    public async Task<ContactRequestSubmitResult> SubmitAsync(SubmitContactRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation.Count > 0)
        {
            return ContactRequestSubmitResult.ValidationFailed(validation);
        }

        var source = ParseSource(request.Source);
        var contactRequest = new ContactRequest(
            source,
            request.FullName,
            request.CompanyName,
            request.PhoneNumber,
            request.Email,
            request.Subject,
            request.Message);

        db.ContactRequests.Add(contactRequest);
        await db.SaveChangesAsync(cancellationToken);

        return ContactRequestSubmitResult.Created(contactRequest.Id);
    }

    public async Task<IReadOnlyList<AdminContactRequestResponse>> ListAdminAsync(
        string? status,
        string? source,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = db.ContactRequests.AsNoTracking();

        if (Enum.TryParse<ContactRequestStatus>(status, ignoreCase: true, out var statusValue))
        {
            query = query.Where(request => request.Status == statusValue);
        }

        if (Enum.TryParse<ContactRequestSource>(source, ignoreCase: true, out var sourceValue))
        {
            query = query.Where(request => request.Source == sourceValue);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(request =>
                request.FullName.ToLower().Contains(term)
                || request.PhoneNumber.ToLower().Contains(term)
                || request.Subject.ToLower().Contains(term)
                || request.Message.ToLower().Contains(term)
                || (request.CompanyName != null && request.CompanyName.ToLower().Contains(term))
                || (request.Email != null && request.Email.ToLower().Contains(term)));
        }

        var requests = await query
            .OrderBy(request => request.Status == ContactRequestStatus.New ? 0 : request.Status == ContactRequestStatus.InProgress ? 1 : 2)
            .ThenByDescending(request => request.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return requests.Select(ToResponse).ToList();
    }

    public async Task<ContactRequestAdminActionResult> MarkInProgressAsync(
        Guid id,
        UpdateContactRequestStatusRequest request,
        CancellationToken cancellationToken)
    {
        var contactRequest = await db.ContactRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contactRequest is null)
        {
            return ContactRequestAdminActionResult.NotFound();
        }

        contactRequest.MarkInProgress(request.Note);
        await db.SaveChangesAsync(cancellationToken);

        return ContactRequestAdminActionResult.Ok(ToResponse(contactRequest));
    }

    public async Task<ContactRequestAdminActionResult> CloseAsync(
        Guid id,
        UpdateContactRequestStatusRequest request,
        CancellationToken cancellationToken)
    {
        var contactRequest = await db.ContactRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (contactRequest is null)
        {
            return ContactRequestAdminActionResult.NotFound();
        }

        contactRequest.Close(request.Note);
        await db.SaveChangesAsync(cancellationToken);

        return ContactRequestAdminActionResult.Ok(ToResponse(contactRequest));
    }

    private static List<string> Validate(SubmitContactRequest request)
    {
        var errors = new List<string>();
        if (!Enum.TryParse<ContactRequestSource>(request.Source, ignoreCase: true, out _))
        {
            errors.Add("Source de contact invalide.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            errors.Add("Renseignez votre nom.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Trim().Length < 6)
        {
            errors.Add("Renseignez un numero de telephone valide.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@', StringComparison.Ordinal))
        {
            errors.Add("L'adresse email semble invalide.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            errors.Add("Selectionnez un sujet.");
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 10)
        {
            errors.Add("Ajoutez un message d'au moins 10 caracteres.");
        }

        return errors;
    }

    private static ContactRequestSource ParseSource(string source)
    {
        return Enum.Parse<ContactRequestSource>(source, ignoreCase: true);
    }

    private static AdminContactRequestResponse ToResponse(ContactRequest request)
    {
        return new AdminContactRequestResponse(
            request.Id,
            request.Source.ToString(),
            request.Status.ToString(),
            request.FullName,
            request.CompanyName,
            request.PhoneNumber,
            request.Email,
            request.Subject,
            request.Message,
            request.AdminNote,
            request.CreatedAt,
            request.ProcessedAt);
    }
}

public sealed record ContactRequestSubmitResult(
    bool IsSuccess,
    Guid? Id,
    IReadOnlyList<string> Errors)
{
    public static ContactRequestSubmitResult Created(Guid id) => new(true, id, []);
    public static ContactRequestSubmitResult ValidationFailed(IReadOnlyList<string> errors) => new(false, null, errors);
}

public sealed record ContactRequestAdminActionResult(
    bool IsSuccess,
    AdminContactRequestResponse? Response,
    string? Message)
{
    public static ContactRequestAdminActionResult Ok(AdminContactRequestResponse response) => new(true, response, null);
    public static ContactRequestAdminActionResult NotFound() => new(false, null, "Demande contact introuvable.");
}
