using System.Net;
using System.Text;
using HomeService.Infrastructure.Location;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeService.Tests.Unit.Infrastructure;

public sealed class GooglePlacesAddressAutocompleteServiceTests
{
    [Fact]
    public async Task SearchAsync_WhenDisabled_DoesNotCallGoogle()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        var service = CreateService(handler, enabled: false, apiKey: "secret");

        var result = await service.SearchAsync("Cocody", "session", CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SearchAsync_ReturnsParsedIvoryCoastSuggestions()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("places:autocomplete", request.RequestUri!.ToString());
            Assert.Equal("secret", request.Headers.GetValues("X-Goog-Api-Key").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"suggestions":[{"placePrediction":{"placeId":"abc","text":{"text":"Riviera 3, Cocody, Abidjan"},"structuredFormat":{"mainText":{"text":"Riviera 3"},"secondaryText":{"text":"Cocody, Abidjan"}}}}]}
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var service = CreateService(handler, enabled: true, apiKey: "secret");

        var result = await service.SearchAsync("Riviera", "session", CancellationToken.None);

        var suggestion = Assert.Single(result);
        Assert.Equal("abc", suggestion.PlaceId);
        Assert.Equal("Riviera 3", suggestion.MainText);
        Assert.Equal("Cocody, Abidjan", suggestion.SecondaryText);
    }

    private static GooglePlacesAddressAutocompleteService CreateService(
        HttpMessageHandler handler,
        bool enabled,
        string? apiKey) =>
        new(
            new HttpClient(handler),
            Options.Create(new GooglePlacesOptions { Enabled = enabled, ApiKey = apiKey }),
            NullLogger<GooglePlacesAddressAutocompleteService>.Instance);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}
