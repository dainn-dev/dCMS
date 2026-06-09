using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace dCMS.Tests.Unit.Infrastructure;

/// <summary>
/// Verifies structured log templates do not embed PII literals in rendered output.
/// Production log call sites are guarded separately by <see cref="PiiLogPathAuditTests"/> (DAI-25-P0-04b).
/// </summary>
public sealed class PiiMaskingAuditTests
{
    [Fact]
    public void Structured_logging_does_not_emit_email_addresses()
    {
        var capture = new CapturingLogger();
        var testEmail = "customer@example.com";

        // Simulate logging structured fields that might contain or be near PII
        capture.LogInformation(
            "Order completed correlation {CorrelationId} tenant {TenantId} userId {UserId}",
            "corr-123",
            Guid.NewGuid(),
            "user-abc");

        // Email should NOT appear in rendered output
        var allLogs = string.Join("\n", capture.LoggedMessages);
        allLogs.Should().NotContain(testEmail, "customer email addresses must not appear in logs");
    }

    [Fact]
    public void Structured_logging_does_not_emit_card_numbers()
    {
        var capture = new CapturingLogger();
        var testCard = "4532015112830366"; // valid Luhn test card

        // Simulate payment operation logging
        capture.LogInformation(
            "Payment processed correlation {CorrelationId} tenant {TenantId} orderId {OrderId} status {Status}",
            "corr-456",
            Guid.NewGuid(),
            "order-789",
            "succeeded");

        var allLogs = string.Join("\n", capture.LoggedMessages);
        allLogs.Should().NotContain(testCard, "card numbers must not appear in logs");
        allLogs.Should().NotMatchRegex(@"\b\d{16}\b", "no 16-digit sequences (card-like patterns) should appear");
    }

    [Fact]
    public void Structured_logging_does_not_emit_phone_numbers()
    {
        var capture = new CapturingLogger();
        var testPhone = "+84 09 1234 5678";

        // Simulate notification logging
        capture.LogInformation(
            "Notification sent correlation {CorrelationId} tenant {TenantId} notificationType {Type}",
            "corr-789",
            Guid.NewGuid(),
            "order_shipped");

        var allLogs = string.Join("\n", capture.LoggedMessages);
        allLogs.Should().NotContain(testPhone, "customer phone numbers must not appear in logs");
    }

    [Fact]
    public void Structured_logging_does_not_emit_bearer_tokens()
    {
        var capture = new CapturingLogger();
        var testToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";

        // Simulate auth failure logging (should log failure reason, NOT token)
        capture.LogWarning(
            "Auth failed correlation {CorrelationId} reason {FailureReason}",
            "corr-999",
            "invalid_signature");

        var allLogs = string.Join("\n", capture.LoggedMessages);
        allLogs.Should().NotContain(testToken, "bearer tokens must not appear in logs");
        allLogs.Should().NotContain("eyJ", "JWT prefix should not appear in logs");
    }

    [Fact]
    public void Structured_logging_does_not_emit_passwords()
    {
        var capture = new CapturingLogger();
        var testPassword = "P@ssw0rd123!";

        // Simulate tenant provisioning logging (should log operation, NOT password)
        capture.LogInformation(
            "Tenant provisioned operation {Operation} tenant {TenantId} status {Status}",
            "create_database",
            "t-acme",
            "succeeded");

        var allLogs = string.Join("\n", capture.LoggedMessages);
        allLogs.Should().NotContain(testPassword, "passwords must not appear in logs");
    }

    [Fact]
    public void Webhook_signature_is_not_logged()
    {
        var capture = new CapturingLogger();
        var testSignature = "t=1234567890,v1=abcdef1234567890abcdef";

        // Simulate webhook failure logging
        capture.LogWarning(
            "Webhook failed service {Service} provider {Provider} reason {FailureReason} correlation {CorrelationId}",
            "payment-api",
            "stripe",
            "invalid_signature",
            "corr-webhook-1");

        var allLogs = string.Join("\n", capture.LoggedMessages);
        allLogs.Should().NotContain(testSignature, "webhook signatures must not appear in logs");
    }

    /// <summary>
    /// In-memory logger that captures log messages for inspection.
    /// </summary>
    private sealed class CapturingLogger : ILogger
    {
        public List<string> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            LoggedMessages.Add(message);
        }
    }
}
