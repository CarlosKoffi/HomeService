namespace HomeService.Company.Mobile.Services;

public sealed record ApiCallResult<T>(bool IsSuccess, T? Response, string? ErrorMessage)
{
    public static ApiCallResult<T> Ok(T response) => new(true, response, null);
    public static ApiCallResult<T> Failed(string message) => new(false, default, message);
}
