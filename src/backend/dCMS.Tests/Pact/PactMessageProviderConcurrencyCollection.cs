using Xunit;

namespace dCMS.Tests.Pact;

internal static class PactTestCollections
{
    internal const string MessageProvider = "PactMessageProvider";
}

/// <summary>
/// PactNet starts an <see cref="System.Net.HttpListener"/> per provider verification; parallel tests can race
/// on the same ephemeral port / URL reservation on Windows.
/// </summary>
[CollectionDefinition(PactTestCollections.MessageProvider)]
public sealed class PactMessageProviderConcurrencyCollection : ICollectionFixture<PactMessageProviderConcurrencyFixture>
{
}

internal sealed class PactMessageProviderConcurrencyFixture
{
}
