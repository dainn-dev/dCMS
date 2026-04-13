using System.Reflection;
using dCMS.Messaging.Contracts;

namespace dCMS.Infrastructure.Messaging;

/// <summary>DAI-304 — RabbitMQ wire headers for envelope metadata (publish + consume validation).</summary>
public static class DcmsMessageEnvelopeHeaders
{
    public const string MessageId = "dcm-message-id";
    public const string Timestamp = "dcm-timestamp";
    public const string TenantId = "dcm-tenant-id";
    public const string MessageType = "dcm-message-type";

    public static string? TryGetMessageTenant(object? message)
    {
        if (message is null)
            return null;

        var p = message.GetType().GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance);
        var v = p?.GetValue(message);
        return v as string;
    }

    public static string GetMessageTypeLabel(object? message)
    {
        if (message is null)
            return "";

        var a = message.GetType().GetCustomAttribute<MessageVersionAttribute>();
        if (a is not null && !string.IsNullOrWhiteSpace(a.Version))
            return a.Version.Trim();

        return message.GetType().Name;
    }
}
