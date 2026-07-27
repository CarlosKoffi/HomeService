using HomeService.Application.Abstractions;
using HomeService.Application.Auditing;
using HomeService.Contracts.Cms;
using HomeService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeService.Application.Admin;

public sealed class AdminCmsContentManagementService(IAppDbContext db)
{
    public async Task<bool> ContentValueExistsAsync(Guid contentValueId, CancellationToken cancellationToken)
    {
        return await db.CmsContentValues
            .AnyAsync(value => value.Id == contentValueId, cancellationToken);
    }

    public async Task<AdminCmsContentManagementResult<CmsContentValueResponse>> UpdateContentValueAsync(
        Guid contentValueId,
        UpdateCmsContentValueRequest request,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var value = await db.CmsContentValues.FirstOrDefaultAsync(item => item.Id == contentValueId, cancellationToken);
        if (value is null)
        {
            return AdminCmsContentManagementResult<CmsContentValueResponse>.NotFound("Champ CMS introuvable.");
        }

        var before = new
        {
            value.TextValue,
            value.JsonValue
        };

        value.SetText(request.TextValue);
        value.SetJson(request.JsonValue);

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            "AdminCmsContentValueUpdated",
            nameof(CmsContentValue),
            value.Id,
            $"Champ CMS '{value.FieldKey}' mis a jour.",
            auditContext,
            before,
            after: new { value.TextValue, value.JsonValue }));

        await db.SaveChangesAsync(cancellationToken);

        return AdminCmsContentManagementResult<CmsContentValueResponse>.Ok(ToResponse(value));
    }

    public async Task<AdminCmsContentManagementResult<CmsMediaUploadResponse>> AttachMediaAsync(
        Guid contentValueId,
        CmsMediaAsset mediaAsset,
        string mediaUrl,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        var value = await db.CmsContentValues.FirstOrDefaultAsync(item => item.Id == contentValueId, cancellationToken);
        if (value is null)
        {
            return AdminCmsContentManagementResult<CmsMediaUploadResponse>.NotFound("Champ CMS introuvable.");
        }

        var before = new
        {
            value.TextValue,
            value.MediaAssetId
        };

        db.CmsMediaAssets.Add(mediaAsset);
        value.AttachMedia(mediaAsset.Id, mediaUrl);

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            "AdminCmsMediaUploaded",
            nameof(CmsContentValue),
            value.Id,
            $"Image CMS '{value.FieldKey}' remplacee.",
            auditContext,
            before,
            after: new { value.TextValue, value.MediaAssetId, mediaAsset.FileName, mediaAsset.SizeInBytes }));

        await db.SaveChangesAsync(cancellationToken);

        return AdminCmsContentManagementResult<CmsMediaUploadResponse>.Ok(new CmsMediaUploadResponse(
            mediaAsset.Id,
            mediaAsset.FileName,
            mediaUrl,
            mediaAsset.ContentType,
            mediaAsset.SizeInBytes));
    }

    public async Task<CmsMediaUploadResponse> AddMediaAsync(
        CmsMediaAsset mediaAsset,
        string mediaUrl,
        AuditActor actor,
        AuditRequestContext? auditContext,
        CancellationToken cancellationToken)
    {
        db.CmsMediaAssets.Add(mediaAsset);

        db.AuditLogEntries.Add(AuditLogFactory.Create(
            actor,
            "AdminCmsMediaAdded",
            nameof(CmsMediaAsset),
            mediaAsset.Id,
            $"Image CMS '{mediaAsset.FileName}' ajoutee.",
            auditContext,
            before: null,
            after: new { mediaAsset.FileName, mediaAsset.ContentType, mediaAsset.SizeInBytes, mediaUrl }));

        await db.SaveChangesAsync(cancellationToken);

        return new CmsMediaUploadResponse(
            mediaAsset.Id,
            mediaAsset.FileName,
            mediaUrl,
            mediaAsset.ContentType,
            mediaAsset.SizeInBytes);
    }

    private static CmsContentValueResponse ToResponse(CmsContentValue value)
    {
        return new CmsContentValueResponse(
            value.Id,
            value.SectionId,
            value.FieldKey,
            value.ValueType.ToString(),
            value.Language?.Code,
            value.TextValue,
            value.JsonValue,
            value.MediaAssetId,
            value.MediaAssetId is null ? null : value.TextValue);
    }
}

public sealed record AdminCmsContentManagementResult<TResponse>(
    AdminCmsContentManagementStatus Status,
    TResponse? Response,
    string? Message)
{
    public static AdminCmsContentManagementResult<TResponse> Ok(TResponse response)
        => new(AdminCmsContentManagementStatus.Ok, response, null);

    public static AdminCmsContentManagementResult<TResponse> NotFound(string message)
        => new(AdminCmsContentManagementStatus.NotFound, default, message);
}

public enum AdminCmsContentManagementStatus
{
    Ok,
    NotFound
}
