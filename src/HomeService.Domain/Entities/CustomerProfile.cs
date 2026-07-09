using HomeService.Domain.Common;

namespace HomeService.Domain.Entities;

public sealed class CustomerProfile : AuditableEntity
{
    private CustomerProfile()
    {
    }

    public CustomerProfile(string firstName, string lastName, string phoneNumber)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber.Trim();
    }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
}
