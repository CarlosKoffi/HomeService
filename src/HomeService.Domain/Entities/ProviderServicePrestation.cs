using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class ProviderServicePrestation : AuditableEntity
{
    private ProviderServicePrestation()
    {
    }

    public ProviderServicePrestation(Guid providerServiceId, Guid servicePrestationId)
    {
        ProviderServiceId = providerServiceId;
        ServicePrestationId = servicePrestationId;
    }

    public Guid ProviderServiceId { get; private set; }
    public ProviderService? ProviderService { get; private set; }
    public Guid ServicePrestationId { get; private set; }
    public ServicePrestation? ServicePrestation { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
