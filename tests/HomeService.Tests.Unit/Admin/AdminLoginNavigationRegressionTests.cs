namespace HomeService.Tests.Unit.Admin;

public sealed class AdminLoginNavigationRegressionTests
{
    [Fact]
    public void Login_page_keeps_navigation_outside_the_sign_in_exception_handler()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagePath = Path.Combine(
            repositoryRoot,
            "src",
            "HomeService.Admin",
            "Components",
            "Pages",
            "AdminLogin.razor");
        var source = File.ReadAllText(pagePath);

        var catchPosition = source.IndexOf("catch (Exception exception)", StringComparison.Ordinal);
        var navigationPosition = source.LastIndexOf("Navigation.NavigateTo(", StringComparison.Ordinal);

        Assert.True(catchPosition >= 0, "The sign-in failure handler was not found.");
        Assert.True(
            navigationPosition > catchPosition,
            "Successful navigation must remain outside the sign-in catch block so Blazor navigation control flow is not reported as a login failure.");
        Assert.Contains("forceLoad: true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_restoration_finishes_before_a_new_login_is_created()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sessionStatePath = Path.Combine(
            repositoryRoot,
            "src",
            "HomeService.Admin",
            "Services",
            "AdminSessionState.cs");
        var source = File.ReadAllText(sessionStatePath);

        var signInPosition = source.IndexOf("public async Task<bool> SignInAsync", StringComparison.Ordinal);
        var initializePosition = source.IndexOf("await InitializeAsync(cancellationToken);", signInPosition, StringComparison.Ordinal);
        var loginPosition = source.IndexOf("LoginAdminAsync", signInPosition, StringComparison.Ordinal);

        Assert.True(signInPosition >= 0, "The admin sign-in method was not found.");
        Assert.True(initializePosition > signInPosition, "Sign-in must wait for initial session restoration.");
        Assert.True(loginPosition > initializePosition, "The API login must run only after session restoration has finished.");
        Assert.Contains("SemaphoreSlim sessionGate", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HomeService.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The HomeService repository root could not be located.");
    }
}
