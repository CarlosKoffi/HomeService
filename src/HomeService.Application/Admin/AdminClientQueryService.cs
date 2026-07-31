using HomeService.Application.Abstractions;
using HomeService.Contracts.Admin;
using HomeService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminClientQueryService(IAppDbContext db)
{
    public async Task<AdminClientListResponse> ListAsync(string? search, CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(customer =>
                customer.FirstName.ToLower().Contains(term)
                || customer.LastName.ToLower().Contains(term)
                || customer.PhoneNumber.ToLower().Contains(term)
                || (customer.Email != null && customer.Email.ToLower().Contains(term)));
        }

        var customers = await query
            .OrderByDescending(customer => customer.CreatedAt)
            .Take(300)
            .Select(customer => new AdminClientSummaryResponse(
                customer.Id,
                (customer.FirstName + " " + customer.LastName).Trim(),
                customer.PhoneNumber,
                customer.Email,
                db.CustomerAddresses
                    .Where(address => address.CustomerId == customer.Id)
                    .OrderByDescending(address => address.IsDefault)
                    .Select(address => address.AddressLine)
                    .FirstOrDefault(),
                db.CustomerAddresses.Count(address => address.CustomerId == customer.Id),
                db.CustomerPaymentMethods.Count(method => method.CustomerId == customer.Id && method.IsActive),
                db.Missions.Count(mission => mission.CustomerId == customer.Id),
                db.Missions.Count(mission => mission.CustomerId == customer.Id && mission.Status == MissionStatus.Completed),
                customer.CreatedAt,
                customer.UpdatedAt))
            .ToListAsync(cancellationToken);

        var stats = new AdminClientStatsResponse(
            await db.Customers.CountAsync(cancellationToken),
            await db.Customers.CountAsync(customer => db.CustomerAddresses.Any(address => address.CustomerId == customer.Id), cancellationToken),
            await db.Customers.CountAsync(customer => db.CustomerPaymentMethods.Any(method => method.CustomerId == customer.Id && method.IsActive), cancellationToken),
            await db.Customers.CountAsync(customer => db.Missions.Any(mission => mission.CustomerId == customer.Id), cancellationToken));

        return new AdminClientListResponse(customers, stats);
    }

    public async Task<AdminClientDetailResponse?> GetAsync(Guid clientId, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(item => item.Id == clientId)
            .Select(item => new
            {
                item.Id,
                item.FirstName,
                item.LastName,
                item.PhoneNumber,
                item.Email,
                item.CreatedAt,
                item.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var addresses = await db.CustomerAddresses.AsNoTracking()
            .Where(address => address.CustomerId == clientId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Label)
            .Select(address => new AdminClientAddressResponse(
                address.Id,
                address.Label,
                address.AddressLine,
                address.Latitude,
                address.Longitude,
                address.IsDefault))
            .ToListAsync(cancellationToken);

        var paymentMethods = await db.CustomerPaymentMethods.AsNoTracking()
            .Where(method => method.CustomerId == clientId)
            .OrderByDescending(method => method.IsDefault)
            .ThenByDescending(method => method.IsActive)
            .Select(method => new AdminClientPaymentMethodResponse(
                method.Id,
                method.Method.ToString(),
                method.Label,
                method.MaskedReference,
                method.IsDefault,
                method.IsActive))
            .ToListAsync(cancellationToken);

        var missions = await (
            from mission in db.Missions.AsNoTracking()
            join service in db.Services.AsNoTracking() on mission.ServiceId equals service.Id
            join prestation in db.ServicePrestations.AsNoTracking() on mission.ServicePrestationId equals prestation.Id into prestations
            from prestation in prestations.DefaultIfEmpty()
            where mission.CustomerId == clientId
            orderby mission.CreatedAt descending
            select new AdminClientMissionResponse(
                mission.Id,
                mission.MissionNumber,
                service.Name,
                prestation == null ? null : prestation.Name,
                mission.Status.ToString(),
                mission.PaymentStatus.ToString(),
                mission.FinalTotalAmount ?? mission.CompanyQuotedAmount ?? mission.EstimatedTotalAmount ?? 0,
                mission.Currency,
                mission.ServiceAddress,
                mission.ScheduledFor,
                mission.CreatedAt))
            .Take(100)
            .ToListAsync(cancellationToken);

        var missionIds = missions.Select(mission => mission.Id).ToList();
        var missionNumbers = missions.ToDictionary(mission => mission.Id, mission => mission.MissionNumber);
        var attachments = await db.MissionAttachments.AsNoTracking()
            .Where(attachment => missionIds.Contains(attachment.MissionId) && !attachment.IsDeleted)
            .OrderByDescending(attachment => attachment.CreatedAt)
            .Select(attachment => new AdminClientAttachmentResponse(
                attachment.Id,
                attachment.MissionId,
                string.Empty,
                attachment.AttachmentType.ToString(),
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.FileSizeBytes,
                attachment.Caption,
                $"/api/admin/client-attachments/{attachment.Id:D}/preview",
                attachment.CreatedAt))
            .ToListAsync(cancellationToken);
        attachments = attachments
            .Select(attachment => attachment with { MissionNumber = missionNumbers.GetValueOrDefault(attachment.MissionId, "Mission") })
            .ToList();

        var notifications = await db.NotificationOutboxMessages.AsNoTracking()
            .Where(notification => notification.OwnerType == MobileDeviceOwnerType.Customer && notification.OwnerId == clientId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(50)
            .Select(notification => new AdminClientNotificationResponse(
                notification.Id,
                notification.Channel.ToString(),
                notification.Status.ToString(),
                notification.Subject,
                notification.Body,
                notification.ScheduledAt,
                notification.SentAt,
                notification.ReadAt))
            .ToListAsync(cancellationToken);

        var reviews = await db.MissionReviews.AsNoTracking()
            .Where(review => review.CustomerId == clientId)
            .Select(review => review.OverallRating)
            .ToListAsync(cancellationToken);

        var activity = new AdminClientActivitySummaryResponse(
            missions.Count,
            missions.Count(mission => mission.Status == MissionStatus.Completed.ToString()),
            missions.Count(mission => mission.Status == MissionStatus.Cancelled.ToString()),
            missions.Count(mission => mission.Status == MissionStatus.Disputed.ToString()),
            reviews.Count,
            reviews.Count == 0 ? null : Math.Round((decimal)reviews.Average(), 1),
            missions.Where(mission => mission.PaymentStatus == PaymentStatus.Paid.ToString()).Sum(mission => mission.Amount),
            missions.FirstOrDefault()?.Currency ?? "XOF");

        return new AdminClientDetailResponse(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            (customer.FirstName + " " + customer.LastName).Trim(),
            customer.PhoneNumber,
            customer.Email,
            customer.CreatedAt,
            customer.UpdatedAt,
            addresses,
            paymentMethods,
            missions,
            attachments,
            notifications,
            activity);
    }

    public async Task<(string StoragePath, string ContentType)?> GetAttachmentFileAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await db.MissionAttachments.AsNoTracking()
            .Where(attachment => attachment.Id == attachmentId && !attachment.IsDeleted)
            .Select(attachment => new { attachment.StoragePath, attachment.ContentType })
            .FirstOrDefaultAsync(cancellationToken);

        return file is null ? null : (file.StoragePath, file.ContentType);
    }
}
