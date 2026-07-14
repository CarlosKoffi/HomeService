using HomeService.Infrastructure;

namespace HomeService.Tests.Unit.Infrastructure;

public sealed class DatabaseConnectionStringResolverTests
{
    [Fact]
    public void Resolve_WhenDirectConnectionIsConfigured_ReturnsDirectConnection()
    {
        var connectionString = "Host=postgres;Port=5432;Database=kaza;Username=kaza;Password=secret";

        var resolved = DatabaseConnectionStringResolver.Resolve(connectionString, null, null);

        Assert.Equal(connectionString, resolved);
    }

    [Fact]
    public void Resolve_WhenDirectConnectionIsPostgresUrl_ConvertsToNpgsqlConnectionString()
    {
        var resolved = DatabaseConnectionStringResolver.Resolve(
            "postgres://home%40service:p%40ss@db.internal:5433/homeservice",
            null,
            null);

        Assert.Contains("Host=db.internal", resolved);
        Assert.Contains("Port=5433", resolved);
        Assert.Contains("Database=homeservice", resolved);
        Assert.Contains("Username=home@service", resolved);
        Assert.Contains("Password=p@ss", resolved);
    }

    [Fact]
    public void Resolve_WhenCoolifyUrlExistsAndDefaultIsLocal_UsesCoolifyUrl()
    {
        var localDefault = "Host=localhost;Port=5432;Database=homeservice;Username=homeservice;Password=homeservice";

        var resolved = DatabaseConnectionStringResolver.Resolve(
            localDefault,
            "postgres://prod:secret@coolify-db:5432/homeservice",
            null);

        Assert.Contains("Host=coolify-db", resolved);
        Assert.Contains("Username=prod", resolved);
        Assert.DoesNotContain("Host=localhost", resolved);
    }

    [Fact]
    public void Resolve_WhenNothingIsConfigured_ThrowsClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnectionStringResolver.Resolve(null, null, null));

        Assert.Contains("DefaultConnection", exception.Message);
    }
}
