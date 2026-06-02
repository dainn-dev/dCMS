using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.AspNetCore.Auth;

/// <summary>
/// DAI-748 (US-1) — verifies the JWT <c>client_id</c> claim matches the deployment's
/// <c>Dcms:Client.Id</c>. A token issued for another chain must never reach a service
/// even if the signing key was reused by accident. <see cref="DcmsRoles.SuperAdmin"/> bypasses.
/// </summary>
/// <remarks>
/// Backward compat: when the token has no <c>client_id</c> claim (issued before US-1),
/// the request is allowed but a single warning is logged so operators can spot stragglers
/// during the rollout window. Once US-2 lands, this filter will be tightened to reject
/// claim-less tokens outright.
/// </remarks>
public sealed class ClientScopeAccessEndpointFilter : IEndpointFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientScopeAccessEndpointFilter> _logger;

    public ClientScopeAccessEndpointFilter(IConfiguration configuration, ILogger<ClientScopeAccessEndpointFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            return await next(context);

        if (user.IsInRole(DcmsRoles.SuperAdmin))
            return await next(context);

        var expected = _configuration.GetSection("Dcms:Client")["Id"]?.Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            // Service started without Dcms:Client.Id — should have been caught at boot.
            // Don't 500 the request here; let the request through and log loudly.
            _logger.LogError(
                "Dcms:Client.Id is not configured for this service. ClientScope check is skipped — fix configuration.");
            return await next(context);
        }

        var tokenClient = user.FindFirst(DcmsClaims.ClientId)?.Value;
        if (string.IsNullOrWhiteSpace(tokenClient))
        {
            // Backward compat path — pre-US-1 tokens. Log once and continue.
            _logger.LogWarning(
                "JWT is missing client_id claim — accepting under US-1 backward-compat. Issuer should be upgraded.");
            return await next(context);
        }

        if (!string.Equals(tokenClient, expected, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Rejecting request: token client_id '{TokenClient}' does not match deployment client '{Expected}'.",
                tokenClient, expected);
            return Results.Json(
                new
                {
                    data = (object?)null,
                    meta = (object?)null,
                    error = new { code = "client_mismatch", message = "Token does not belong to this deployment's client." }
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
