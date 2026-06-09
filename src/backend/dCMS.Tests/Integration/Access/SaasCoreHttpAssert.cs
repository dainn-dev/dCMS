using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace dCMS.Tests.Integration.Access;

public static class SaasCoreHttpAssert
{
    public static async Task AssertAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedErrorCode = null)
    {
        response.StatusCode.Should().Be(expectedStatus, await response.Content.ReadAsStringAsync());

        if (string.IsNullOrWhiteSpace(expectedErrorCode))
            return;

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(expectedErrorCode, because: "error envelope should include expected code");
    }

    public static async Task<string?> TryReadErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("code", out var code))
                    return code.GetString();
            }
        }
        catch
        {
            // envelope may differ on some legacy routes
        }

        return null;
    }
}
