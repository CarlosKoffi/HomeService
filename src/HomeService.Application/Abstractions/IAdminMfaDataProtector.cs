namespace HomeService.Application.Abstractions;

public interface IAdminMfaDataProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
