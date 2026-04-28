using System.Collections.Concurrent;
using System.Text.Json;
using dCMS.Notification.Api.Routes;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace dCMS.Notification.Api.Rendering;

public sealed class ScribanTemplateRenderer : ITemplateRenderer
{
    private readonly TemplateRepository _repo;
    private readonly ILogger<ScribanTemplateRenderer> _log;
    private readonly ConcurrentDictionary<string, (Template? subject, Template body)> _cache = new(StringComparer.Ordinal);

    public ScribanTemplateRenderer(TemplateRepository repo, ILogger<ScribanTemplateRenderer> log)
    {
        _repo = repo;
        _log = log;
    }

    public async Task<RenderResult> RenderAsync(
        string tenantId,
        string key,
        string locale,
        string channel,
        object model,
        CancellationToken ct)
    {
        // Default locale fallback is currently fixed; can be extended per-tenant settings later.
        const string defaultLocale = "en-US";

        var row = await _repo.GetResolvedAsync(tenantId, key, locale, channel, defaultLocale, ct).ConfigureAwait(false);
        if (row is null)
            return new RenderResult(Subject: null, Body: $"{key} notification", Channel: channel);

        try
        {
            var cacheKey = $"{row.TenantId ?? "global"}|{row.Key}|{row.Locale}|{row.Channel}|v{row.ModelVersion}|{row.UpdatedAt:O}";
            var compiled = _cache.GetOrAdd(cacheKey, _ =>
            {
                Template? subj = null;
                if (!string.IsNullOrWhiteSpace(row.Subject))
                {
                    subj = Template.Parse(row.Subject);
                    if (subj.HasErrors) throw new InvalidOperationException("Subject template parse failed: " + string.Join("; ", subj.Messages));
                }
                var body = Template.Parse(row.Body);
                if (body.HasErrors) throw new InvalidOperationException("Body template parse failed: " + string.Join("; ", body.Messages));
                return (subj, body);
            });

            var ctx = CreateSandboxedContext(model, ct);
            var renderedSubject = compiled.subject is null ? row.Subject : compiled.subject.Render(ctx);
            var renderedBody = compiled.body.Render(ctx);
            return new RenderResult(renderedSubject, renderedBody, row.Channel);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Template render failed for {Key}/{Locale}/{Channel} (tenant {TenantId})", key, locale, channel, tenantId);
            return new RenderResult(Subject: row.Subject, Body: $"{key} notification", Channel: channel);
        }
    }

    private static TemplateContext CreateSandboxedContext(object model, CancellationToken ct)
    {
        // Sandbox hardening:
        // - don't expose .NET objects directly (pass JSON model only)
        // - set loop/recursion limits
        // - propagate cancellation token (best-effort)
        var ctx = new TemplateContext
        {
            EnableRelaxedMemberAccess = false,
            LoopLimit = 10_000,
            RecursiveLimit = 128,
        };

        if (model is JsonElement je)
        {
            var root = ScribanModel.FromJsonElement(je);
            // Helpers (safe pure functions)
            root.Add("format_currency", new Func<decimal, string, string>((amount, currency) => $"{amount:0.00} {currency}"));
            root.Add("format_date", new Func<string, string>(s => s));
            root.Add("url", new Func<string, string>(s => s));
            ctx.PushGlobal(root);
        }
        else
        {
            // Fallback: no model
            var root = new ScriptObject();
            root.Add("model", new ScriptObject());
            ctx.PushGlobal(root);
        }

        // best-effort cancellation
        ctx.CancellationToken = ct;
        return ctx;
    }
}

