namespace dCMS.Messaging.Contracts;

/// <summary>Transport envelope metadata (DAI-303). Payload may be the message body or a nested DTO.</summary>
public interface IMessageEnvelope
{
    Guid MessageId { get; }

    DateTimeOffset Timestamp { get; }

    /// <summary>Tenant / Siêu thị scope; required for multi-tenant validation on consume.</summary>
    string TenantId { get; }

    /// <summary>Logical message name + version (often from <see cref="MessageVersionAttribute"/>).</summary>
    string MessageType { get; }
}
