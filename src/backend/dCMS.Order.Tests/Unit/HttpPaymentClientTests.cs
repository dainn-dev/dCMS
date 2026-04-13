using System.Net;
using System.Text;
using dCMS.Order.Core.Integration;
using dCMS.Order.Infrastructure.Integration;
using Microsoft.Extensions.Logging.Abstractions;

namespace dCMS.Order.Tests.Unit;

public sealed class HttpPaymentClientTests
{
    [Fact]
    public async Task CreatePaymentIntentAsync_reads_data_envelope()
    {
        const string json = """{"data":{"paymentIntentId":"pi_abc","paymentUrl":"https://pay.example/x"},"error":null}""";
        using var handler = new StubHandler(_ => OkJson(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://payment.test/") };
        var sut = new HttpPaymentClient(http, NullLogger<HttpPaymentClient>.Instance);

        var r = await sut.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest("ord-1", "t1", "s1", "c1", 10m, "USD"));

        Assert.Equal("pi_abc", r.PaymentIntentId);
        Assert.Equal("https://pay.example/x", r.PaymentUrl);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_reads_flat_body()
    {
        const string json = """{"paymentIntentId":"pi_flat","paymentUrl":"https://flat"}""";
        using var handler = new StubHandler(_ => OkJson(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://payment.test/") };
        var sut = new HttpPaymentClient(http, NullLogger<HttpPaymentClient>.Instance);

        var r = await sut.CreatePaymentIntentAsync(
            new CreatePaymentIntentRequest("o", "t", "s", "c", 1m, "USD"));

        Assert.Equal("pi_flat", r.PaymentIntentId);
        Assert.Equal("https://flat", r.PaymentUrl);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_when_error_envelope_throws_PaymentInitException()
    {
        const string json = """{"data":null,"error":{"code":"DECLINED","message":"card failed"}}""";
        using var handler = new StubHandler(_ => OkJson(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://payment.test/") };
        var sut = new HttpPaymentClient(http, NullLogger<HttpPaymentClient>.Instance);

        var ex = await Assert.ThrowsAsync<PaymentInitException>(() =>
            sut.CreatePaymentIntentAsync(new CreatePaymentIntentRequest("o", "t", "s", "c", 1m, "USD")));

        Assert.Contains("DECLINED", ex.Message, StringComparison.Ordinal);
        Assert.Equal("DECLINED", ex.ServiceErrorCode);
    }

    [Fact]
    public async Task CreatePaymentIntentAsync_when_http_error_throws_PaymentInitException()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream", Encoding.UTF8, "text/plain"),
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://payment.test/") };
        var sut = new HttpPaymentClient(http, NullLogger<HttpPaymentClient>.Instance);

        var ex = await Assert.ThrowsAsync<PaymentInitException>(() =>
            sut.CreatePaymentIntentAsync(new CreatePaymentIntentRequest("o", "t", "s", "c", 1m, "USD")));

        Assert.Equal("HTTP_ERROR", ex.ServiceErrorCode);
        Assert.Contains("502", ex.Message, StringComparison.Ordinal);
    }

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_send(request));
    }
}
