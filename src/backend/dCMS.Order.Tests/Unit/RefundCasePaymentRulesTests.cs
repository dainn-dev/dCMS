using dCMS.Order.Core.Ordering;

namespace dCMS.Order.Tests.Unit;

public sealed class RefundCasePaymentRulesTests
{
    [Theory]
    [InlineData("succeeded")]
    [InlineData("SUCCEEDED")]
    [InlineData("refunded")]
    [InlineData("initiated")]
    [InlineData("completed")]
    public void IsQualifyingLatestTransactionStatus_accepts_expected_gateways(string status) =>
        Assert.True(RefundCasePaymentRules.IsQualifyingLatestTransactionStatus(status));

    [Theory]
    [InlineData("failed")]
    [InlineData("pending")]
    [InlineData("")]
    [InlineData(null)]
    public void IsQualifyingLatestTransactionStatus_rejects_non_qualifying(string? status) =>
        Assert.False(RefundCasePaymentRules.IsQualifyingLatestTransactionStatus(status));
}
