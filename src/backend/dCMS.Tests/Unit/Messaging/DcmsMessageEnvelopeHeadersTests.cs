using dCMS.Core.Messaging;
using dCMS.Infrastructure.Messaging;

namespace dCMS.Tests.Unit.Messaging;

public sealed class DcmsMessageEnvelopeHeadersTests
{
    [Fact]
    public void TryGetMessageTenant_reads_TenantId_property()
    {
        var m = new PaymentCompletedV1("o", "p", "tenant-guid", "s", DateTimeOffset.UtcNow);
        Assert.Equal("tenant-guid", DcmsMessageEnvelopeHeaders.TryGetMessageTenant(m));
    }

    [Fact]
    public void GetMessageTypeLabel_uses_MessageVersion_attribute_when_present()
    {
        var m = new OrderPlacedV1(
            "o",
            "t",
            "s",
            "c",
            1m,
            "USD",
            [new OrderPlacedLineV1("v", "w", 1)],
            DateTimeOffset.UtcNow);
        Assert.Equal("OrderPlaced.v1", DcmsMessageEnvelopeHeaders.GetMessageTypeLabel(m));
    }

    [Fact]
    public void GetMessageTypeLabel_falls_back_to_clr_name_without_attribute()
    {
        var m = new StockReservationTimeoutV1(Guid.NewGuid().ToString());
        Assert.Equal(nameof(StockReservationTimeoutV1), DcmsMessageEnvelopeHeaders.GetMessageTypeLabel(m));
    }
}
