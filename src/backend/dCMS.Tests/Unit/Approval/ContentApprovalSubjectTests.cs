using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using dCMS.Approval.Api.Routes.Subjects;
using dCMS.Core.Approvals;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Approval;

public sealed class ContentApprovalSubjectTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(StatusCode));
        }
    }

    private sealed class StaticFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StaticFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static JsonDocument Empty() => JsonDocument.Parse("{}");

    private static (ContentApprovalSubject subject, CapturingHandler handler) Make(ContentApprovalCallbackOptions opts)
    {
        var handler = new CapturingHandler();
        var subject = new ContentApprovalSubject(new StaticFactory(handler), opts);
        return (subject, handler);
    }

    [Fact]
    public void EntityType_is_Content()
    {
        var (subject, _) = Make(new ContentApprovalCallbackOptions());
        subject.EntityType.Should().Be("Content");
    }

    [Fact]
    public async Task ValidateAsync_rejects_non_guid_entityId()
    {
        var (subject, _) = Make(new ContentApprovalCallbackOptions());
        using var doc = Empty();
        var err = await subject.ValidateAsync("t1", "not-a-guid", ApprovalAction.Approve, doc, default);
        err.Should().Contain("GUID");
    }

    [Fact]
    public async Task ValidateAsync_accepts_guid_entityId()
    {
        var (subject, _) = Make(new ContentApprovalCallbackOptions());
        using var doc = Empty();
        var err = await subject.ValidateAsync("t1", Guid.NewGuid().ToString(), ApprovalAction.Approve, doc, default);
        err.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_is_noop_when_callback_not_configured()
    {
        var (subject, handler) = Make(new ContentApprovalCallbackOptions());
        using var doc = Empty();
        await subject.ApplyAsync("t1", Guid.NewGuid().ToString(), ApprovalAction.Approve, doc, "u1", default);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyAsync_Approve_calls_publish_endpoint_with_api_key_header()
    {
        var opts = new ContentApprovalCallbackOptions
        {
            CallbackUrl = "http://web/umbraco/dcms/api/content-approval",
            ApiKey = "secret-key",
        };
        var (subject, handler) = Make(opts);
        using var doc = Empty();
        var key = Guid.NewGuid().ToString();

        await subject.ApplyAsync("t1", key, ApprovalAction.Approve, doc, "u1", default);

        handler.Requests.Should().HaveCount(1);
        var req = handler.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.AbsoluteUri.Should().EndWith("/publish");
        req.Headers.GetValues("X-Internal-Api-Key").Should().ContainSingle().Which.Should().Be("secret-key");
    }

    [Fact]
    public async Task ApplyAsync_Reject_calls_unpublish_endpoint()
    {
        var opts = new ContentApprovalCallbackOptions { CallbackUrl = "http://web/cb", ApiKey = "k" };
        var (subject, handler) = Make(opts);
        using var doc = Empty();

        await subject.ApplyAsync("t1", Guid.NewGuid().ToString(), ApprovalAction.Reject, doc, "u1", default);

        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].RequestUri!.AbsoluteUri.Should().EndWith("/unpublish");
    }

    [Fact]
    public async Task ApplyAsync_Submit_does_not_call_callback()
    {
        var opts = new ContentApprovalCallbackOptions { CallbackUrl = "http://web/cb", ApiKey = "k" };
        var (subject, handler) = Make(opts);
        using var doc = Empty();

        await subject.ApplyAsync("t1", Guid.NewGuid().ToString(), ApprovalAction.Submit, doc, "u1", default);

        handler.Requests.Should().BeEmpty();
    }
}
