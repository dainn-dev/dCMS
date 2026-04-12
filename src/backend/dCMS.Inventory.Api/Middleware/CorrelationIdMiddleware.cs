namespace dCMS.Inventory.Api.Middleware;

public static class CorrelationIdMiddlewareExtensions
{
    public const string HeaderName = "X-Correlation-Id";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            string correlationId;
            if (context.Request.Headers.TryGetValue(HeaderName, out var existing))
            {
                var raw = existing.ToString().Trim();
                correlationId = string.IsNullOrWhiteSpace(raw) ? Guid.NewGuid().ToString("N") : raw;
                if (correlationId.Length > 128)
                    correlationId = correlationId[..128];
            }
            else
                correlationId = Guid.NewGuid().ToString("N");

            context.Response.Headers[HeaderName] = correlationId;
            context.Items["CorrelationId"] = correlationId;
            await next().ConfigureAwait(false);
        });
}
