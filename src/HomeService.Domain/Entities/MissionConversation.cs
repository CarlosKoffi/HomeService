using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class MissionConversation : AuditableEntity
{
    private readonly List<MissionMessage> _messages = [];

    private MissionConversation()
    {
    }

    public MissionConversation(Guid missionId, Guid? providerId, Guid? companyId, Guid customerId)
    {
        MissionId = missionId;
        ProviderId = providerId;
        CompanyId = companyId;
        CustomerId = customerId;
    }

    public Guid MissionId { get; private set; }
    public Mission? Mission { get; private set; }
    public Guid? ProviderId { get; private set; }
    public ProviderProfile? Provider { get; private set; }
    public Guid? CompanyId { get; private set; }
    public Company? Company { get; private set; }
    public Guid CustomerId { get; private set; }
    public CustomerProfile? Customer { get; private set; }
    public IReadOnlyCollection<MissionMessage> Messages => _messages;
}
