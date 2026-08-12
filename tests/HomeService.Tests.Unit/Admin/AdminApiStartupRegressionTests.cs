namespace HomeService.Tests.Unit.Admin;

public sealed class AdminApiStartupRegressionTests
{
    [Fact]
    public void Financial_role_bootstrap_does_not_translate_dictionary_keys_with_npgsql()
    {
        var repositoryRoot = FindRepositoryRoot();
        var initializerPath = Path.Combine(
            repositoryRoot,
            "src",
            "HomeService.Api",
            "DatabaseInitializer.cs");
        var source = File.ReadAllText(initializerPath);

        Assert.DoesNotContain(
            ".Where(item => allowed.Keys.Contains(item.Key))",
            source,
            StringComparison.Ordinal);
        Assert.Contains("allowedModuleKeys = allowed.Keys.ToHashSet()", source, StringComparison.Ordinal);
        Assert.Contains(".ToListAsync(cancellationToken)", source, StringComparison.Ordinal);
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
