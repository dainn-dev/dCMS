using System.Threading.Channels;
using dCMS.Core.Audit;

namespace dCMS.Infrastructure.Audit;

/// <summary>Fire-and-forget bounded queue for audit rows (US-11).</summary>
public sealed class AuditLogChannel
{
    private readonly Channel<AuditLogEntry> _channel = Channel.CreateBounded<AuditLogEntry>(new BoundedChannelOptions(4096)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public bool TryWrite(AuditLogEntry entry) => _channel.Writer.TryWrite(entry);

    public ChannelReader<AuditLogEntry> Reader => _channel.Reader;
}
