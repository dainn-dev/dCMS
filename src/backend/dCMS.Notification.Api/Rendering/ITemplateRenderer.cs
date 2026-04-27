namespace dCMS.Notification.Api.Rendering;

public interface ITemplateRenderer
{
    Task<RenderResult> RenderAsync(
        string tenantId,
        string key,
        string locale,
        string channel,
        object model,
        CancellationToken ct);
}

public sealed record RenderResult(string? Subject, string Body, string Channel);

