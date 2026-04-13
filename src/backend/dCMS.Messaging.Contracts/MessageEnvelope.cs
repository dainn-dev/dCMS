namespace dCMS.Messaging.Contracts;

/// <summary>Generic envelope wrapping a typed payload (optional pattern for versioned transports).</summary>
public sealed record MessageEnvelope<TPayload>(
    Guid MessageId,
    DateTimeOffset Timestamp,
    string TenantId,
    string MessageType,
    TPayload Payload) : IMessageEnvelope;
