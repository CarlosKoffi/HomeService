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
