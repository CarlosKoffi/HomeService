namespace HomeService.Application.Abstractions;

public interface IPayoutDataProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
