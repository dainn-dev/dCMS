using System.Text.Json;
using PactNet.Verifier;
using PactNet.Verifier.Messaging;
using Xunit;

namespace dCMS.Tests.Pact;

/// <summary>Shared PactNet 5 message-provider setup (HTTP dummy transport + JSON + metadata).</summary>
internal static class PactNetMessageProviderSupport
{
    internal static readonly JsonSerializerOptions JsonCamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static string PactFilePath(string pactFileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts", pactFileName));

    /// <summary>
    /// PactNet 5.0.0: message-only verification sets scheme <c>message</c> and the Rust verifier fails
    /// (<c>builder error for url (message://...)</c>). Priming with HTTP first is the supported workaround.
    /// Host must be <c>localhost</c> to match <see cref="PactNet.Verifier.Messaging.MessagingProvider"/> prefixes.
    /// See https://github.com/pact-foundation/pact-net/issues/530
    /// </summary>
    internal static void VerifyMessagePactFile(
        string providerName,
        string pactFileName,
        Action<IMessageScenarios> configureScenarios)
    {
        var path = PactFilePath(pactFileName);
        Assert.True(File.Exists(path), $"Run the consumer pact test first; missing {path}");

        using var verifier = new PactVerifier(providerName);
        verifier
            .WithHttpEndpoint(new Uri("http://localhost:9"))
            .WithMessages(
                scenarios => configureScenarios(scenarios),
                JsonCamelCase)
            .WithFileSource(new FileInfo(path))
            .Verify();
    }

    internal static void VerifyMessageContract(
        string providerName,
        string interactionDescription,
        Func<object> messageBody,
        string pactFileName) =>
        VerifyMessagePactFile(
            providerName,
            pactFileName,
            scenarios => scenarios.Add(
                interactionDescription,
                builder =>
                {
                    builder.WithMetadata(new { ContentType = "application/json" });
                    builder.WithContent(messageBody, JsonCamelCase);
                }));
}
