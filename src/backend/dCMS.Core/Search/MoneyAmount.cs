using System.Text.Json.Serialization;

namespace dCMS.Core.Search;

/// <summary>Money shape aligned with ES mapping (<c>amount</c> + <c>currency</c>).</summary>
public sealed record MoneyAmount(
    [property: JsonPropertyName("amount")] long Amount,
    [property: JsonPropertyName("currency")] string Currency);
