using System.Buffers.Binary;

namespace dCMS.Infrastructure.Outbox;

/// <summary>Deterministic <see cref="Guid"/> for outbox rows so redelivery reuses the same transport message id.</summary>
public static class OutboxPublishMessageId
{
    /// <summary>ASCII <c>DCMSOTBx</c> — marks ids derived from outbox rows.</summary>
    private const ulong Magic = 0x44434D534F544278UL;

    public static Guid FromOutboxRow(long outboxEventId)
    {
        Span<byte> b = stackalloc byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(b, outboxEventId);
        BinaryPrimitives.WriteUInt64LittleEndian(b[8..], Magic);
        return new Guid(b);
    }
}
