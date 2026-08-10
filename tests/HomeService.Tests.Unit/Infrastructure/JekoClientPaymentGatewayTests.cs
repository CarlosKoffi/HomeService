using System.Net;
using System.Text;
using System.Text.Json;
using HomeService.Application.Abstractions;
using HomeService.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace HomeService.Tests.Unit.Infrastructure;

public sealed class JekoClientPaymentGatewayTests
{
    [Fact]
    public async Task CreateAsync_SendsTheDocumentedJekoRedirectContract()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""
                {
                  "id": "payreq_123",
                  "reference": "WELE-MIS-42-ABC",
                  "type": "redirect",
                  "paymentMethod": "orange",
                  "status": "pending",
                  "redirectUrl": "https://pay.jeko.africa/payreq_123"
                }
                """);
        });
        var gateway = CreateGateway(handler);
        var localId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        var result = await gateway.CreateAsync(
            new ClientPaymentGatewayRequest(
                localId,
                missionId,
                "WELE-MIS-42-ABC",
                "orange",
                21_828,
                "XOF"),
            CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("pending", result.Status);
        Assert.Equal("payreq_123", result.ExternalPaymentRequestId);
        Assert.Equal("https://pay.jeko.africa/payreq_123", result.RedirectUrl);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://api.jeko.africa/partner_api/payment_requests", capturedRequest.RequestUri!.ToString());
        Assert.Equal("secret-key", Assert.Single(capturedRequest.Headers.GetValues("X-API-KEY")));
        Assert.Equal("key-id", Assert.Single(capturedRequest.Headers.GetValues("X-API-KEY-ID")));

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal("store-1", root.GetProperty("storeId").GetString());
        Assert.Equal(21_828, root.GetProperty("amountCents").GetInt32());
        Assert.Equal("XOF", root.GetProperty("currency").GetString());
        Assert.Equal("WELE-MIS-42-ABC", root.GetProperty("reference").GetString());
        var paymentDetails = root.GetProperty("paymentDetails");
        Assert.Equal("redirect", paymentDetails.GetProperty("type").GetString());
        var data = paymentDetails.GetProperty("data");
        Assert.Equal("orange", data.GetProperty("paymentMethod").GetString());
        Assert.Equal(
            $"https://api.wele.africa/api/webhooks/jeko/return?paymentRequestId={localId:D}&missionId={missionId:D}&result=success",
            data.GetProperty("successUrl").GetString());
        Assert.Equal(
            $"https://api.wele.africa/api/webhooks/jeko/return?paymentRequestId={localId:D}&missionId={missionId:D}&result=error",
            data.GetProperty("errorUrl").GetString());
    }

    [Fact]
    public async Task GetStatusAsync_ReadsSuccessfulAmountAndCurrency()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(JsonResponse("""
            {
              "data": {
                "id": "payreq_123",
                "transactionId": "txn_456",
                "status": "success",
                "amount": {
                  "amount": 21828,
                  "currency": "XOF"
                }
              }
            }
            """)));
        var gateway = CreateGateway(handler);

        var result = await gateway.GetStatusAsync("payreq_123", CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(result.IsDefinitive);
        Assert.Equal("success", result.Status);
        Assert.Equal("payreq_123", result.ExternalPaymentRequestId);
        Assert.Equal("txn_456", result.ExternalTransactionId);
        Assert.Equal(21_828, result.Amount);
        Assert.Equal("XOF", result.Currency);
    }

    private static JekoClientPaymentGateway CreateGateway(HttpMessageHandler handler)
    {
        var configuration = new DictionaryConfiguration(
            new Dictionary<string, string?>
            {
                ["JEKO_PAYMENTS_ENABLED"] = "true",
                ["JEKO_API_BASE_URL"] = "https://api.jeko.africa",
                ["JEKO_API_KEY"] = "secret-key",
                ["JEKO_API_KEY_ID"] = "key-id",
                ["JEKO_STORE_ID"] = "store-1",
                ["JEKO_PAYMENT_RETURN_BASE_URL"] = "https://api.wele.africa",
                ["JEKO_PAYMENT_FEE_RATE_BASIS_POINTS"] = "150"
            });
        return new JekoClientPaymentGateway(
            new HttpClient(handler),
            configuration,
            NullLogger<JekoClientPaymentGateway>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);
    }

    private sealed class DictionaryConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.GetValueOrDefault(key);
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => NeverChangeToken.Instance;

        public IConfigurationSection GetSection(string key) =>
            throw new NotSupportedException($"La section {key} n'est pas utilisee par ce test.");
    }

    private sealed class NeverChangeToken : IChangeToken
    {
        public static NeverChangeToken Instance { get; } = new();
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}
