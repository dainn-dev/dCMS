using dCMS.Order.Core.Ordering;

namespace dCMS.Order.Tests.Unit;

public sealed class OrderListCursorCodecTests
{
    [Fact]
    public void TryDecode_empty_cursor_is_valid_first_page()
    {
        var ok = OrderListCursorCodec.TryDecode(null, out var ca, out var id);
        Assert.True(ok);
        Assert.Null(ca);
        Assert.Null(id);

        ok = OrderListCursorCodec.TryDecode("   ", out ca, out id);
        Assert.True(ok);
        Assert.Null(ca);
        Assert.Null(id);
    }

    [Fact]
    public void Encode_round_trips_createdAt_and_id()
    {
        var created = new DateTimeOffset(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);
        var gid = Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");
        var enc = OrderListCursorCodec.Encode(created, gid);
        Assert.DoesNotContain('+', enc);
        Assert.DoesNotContain('/', enc);

        var ok = OrderListCursorCodec.TryDecode(enc, out var ca, out var id);
        Assert.True(ok);
        Assert.Equal(created, ca);
        Assert.Equal(gid, id);
    }

    [Theory]
    [InlineData("not-base64!!!")]
    [InlineData("abcd")] // valid base64 but garbage payload
    public void TryDecode_invalid_returns_false(string bad)
    {
        var ok = OrderListCursorCodec.TryDecode(bad, out _, out _);
        Assert.False(ok);
    }
}
