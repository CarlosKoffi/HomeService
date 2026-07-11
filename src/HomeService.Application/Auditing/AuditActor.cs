using HomeService.Domain.Enums;

namespace HomeService.Application.Auditing;

public sealed record AuditActor(AuditActorType Type, Guid? Id, string? DisplayName)
{
    public static AuditActor Admin(string? displayName = "admin") => new(AuditActorType.Admin, null, displayName);

    public static AuditActor Company(Guid companyId, string? displayName) => new(AuditActorType.Company, companyId, displayName);

    public static AuditActor Provider(Guid providerId, string? displayName) => new(AuditActorType.Provider, providerId, displayName);

    public static AuditActor Anonymous(string? displayName = null) => new(AuditActorType.Anonymous, null, displayName);

    public static AuditActor System(string? displayName = "system") => new(AuditActorType.System, null, displayName);
}
