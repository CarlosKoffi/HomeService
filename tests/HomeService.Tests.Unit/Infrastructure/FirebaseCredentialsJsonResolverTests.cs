using System.Text;
using HomeService.Infrastructure.Notifications;

namespace HomeService.Tests.Unit.Infrastructure;

public sealed class FirebaseCredentialsJsonResolverTests
{
    [Fact]
    public void Resolve_WhenBase64IsConfigured_ReturnsDecodedJson()
    {
        const string json = """{"project_id":"homeservice"}""";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var result = FirebaseCredentialsJsonResolver.Resolve(null, encoded);

        Assert.Equal(json, result);
    }

    [Fact]
    public void Resolve_WhenBase64IsInvalid_FallsBackToRawJson()
    {
        const string json = """{"project_id":"fallback"}""";

        var result = FirebaseCredentialsJsonResolver.Resolve(json, "not-base64");

        Assert.Equal(json, result);
    }

    [Fact]
    public void Resolve_WhenFirstBase64CandidateIsEmpty_ReturnsNextDecodedJson()
    {
        const string json = """{"project_id":"homeservice"}""";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var result = FirebaseCredentialsJsonResolver.Resolve(null, null, encoded);

        Assert.Equal(json, result);
    }

    [Fact]
    public void Resolve_WhenFirstBase64CandidateIsInvalid_ReturnsNextDecodedJson()
    {
        const string json = """{"project_id":"homeservice"}""";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var result = FirebaseCredentialsJsonResolver.Resolve(null, "not-base64", encoded);

        Assert.Equal(json, result);
    }
}
