using System.Text.Json;
using Dapper;
using dCMS.Infrastructure.Messaging;
using dCMS.Messaging.Contracts.Messaging;
using dCMS.Notification.Api.Routes;
using MailKit.Net.Smtp;
using MailKit.Security;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Npgsql;
using Polly;
using System.Data;

namespace dCMS.Notification.Worker.Consumers;

public sealed class EmailQueuedConsumer : IConsumer<EmailQueuedV1>
{
    private readonly string _notificationCs;
    private readonly IIdempotencyService _idempotency;
    private readonly dCMS.Notification.Api.Rendering.ITemplateRenderer _renderer;
    private readonly ILogger<EmailQueuedConsumer> _log;
    private readonly IConfiguration _cfg;

    public EmailQueuedConsumer(
        IConfiguration cfg,
        IIdempotencyService idempotency,
        dCMS.Notification.Api.Rendering.ITemplateRenderer renderer,
        ILogger<EmailQueuedConsumer> log)
    {
        _cfg = cfg;
        _notificationCs = cfg.GetConnectionString("Notification")
            ?? throw new InvalidOperationException("ConnectionStrings:Notification is required.");
        _idempotency = idempotency;
        _renderer = renderer;
        _log = log;
    }

    public async Task Consume(ConsumeContext<EmailQueuedV1> context)
    {
        var m = context.Message;
        var messageId = context.MessageId?.ToString() ?? $"email:{m.TenantId}:{m.IdempotencyKey}";

        await using var _ = await _idempotency.AcquireOrderingLockAsync(messageId, context.CancellationToken).ConfigureAwait(false);
        if (await _idempotency.IsProcessedAsync(messageId, context.CancellationToken).ConfigureAwait(false))
            return;

        var now = DateTimeOffset.UtcNow;
        await EnsureDeliveryRowAsync(m, now, context.CancellationToken).ConfigureAwait(false);

        try
        {
            using var doc = JsonDocument.Parse(m.ModelJson);
            var render = await _renderer.RenderAsync(m.TenantId, m.TemplateKey, m.Locale, "email", doc.RootElement.Clone(), context.CancellationToken)
                .ConfigureAwait(false);

            await SendEmailAsync(m.ToAddress, render.Subject ?? m.TemplateKey, render.Body, context.CancellationToken).ConfigureAwait(false);
            await MarkSentAsync(m, now, context.CancellationToken).ConfigureAwait(false);

            await _idempotency.MarkProcessedAsync(messageId, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MarkFailedAsync(m, ex.Message, context.CancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task EnsureDeliveryRowAsync(EmailQueuedV1 m, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO "EmailDeliveries"
                ("Id","TenantId","StoreId","IdempotencyKey","TemplateKey","Locale","ToAddress","Status","Error","CreatedAt","SentAt")
            VALUES
                (@Id,@TenantId,@StoreId,@IdempotencyKey,@TemplateKey,@Locale,@ToAddress,'queued',NULL,@CreatedAt,NULL)
            ON CONFLICT ("TenantId","IdempotencyKey") DO NOTHING
            """;
        await using var conn = new NpgsqlConnection(_notificationCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(sql, new
        {
            Id = Guid.NewGuid(),
            TenantId = m.TenantId,
            StoreId = m.StoreId,
            m.IdempotencyKey,
            m.TemplateKey,
            m.Locale,
            m.ToAddress,
            CreatedAt = now,
        }).ConfigureAwait(false);
    }

    private async Task MarkSentAsync(EmailQueuedV1 m, DateTimeOffset now, CancellationToken ct)
    {
        const string sql = """
            UPDATE "EmailDeliveries"
            SET "Status" = 'sent', "SentAt" = @SentAt, "Error" = NULL
            WHERE "TenantId" = @TenantId AND "IdempotencyKey" = @IdempotencyKey
            """;
        await using var conn = new NpgsqlConnection(_notificationCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { TenantId = m.TenantId, m.IdempotencyKey, SentAt = now }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private async Task MarkFailedAsync(EmailQueuedV1 m, string error, CancellationToken ct)
    {
        const string sql = """
            UPDATE "EmailDeliveries"
            SET "Status" = 'failed', "Error" = LEFT(@Error, 4000)
            WHERE "TenantId" = @TenantId AND "IdempotencyKey" = @IdempotencyKey
            """;
        await using var conn = new NpgsqlConnection(_notificationCs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { TenantId = m.TenantId, m.IdempotencyKey, Error = error }, cancellationToken: ct))
            .ConfigureAwait(false);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        var host = _cfg["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _log.LogWarning("SMTP is not configured (Smtp:Host missing). Skipping send to {To}", to);
            return;
        }

        var port = int.TryParse(_cfg["Smtp:Port"], out var p) ? p : 587;
        var user = _cfg["Smtp:User"];
        var pass = _cfg["Smtp:Pass"];
        var from = _cfg["Smtp:From"] ?? "no-reply@dcms.local";

        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(from));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        var retry = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(i * 2));

        await retry.ExecuteAsync(async () =>
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTlsWhenAvailable, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(user))
                await client.AuthenticateAsync(user, pass ?? "", ct).ConfigureAwait(false);
            await client.SendAsync(msg, ct).ConfigureAwait(false);
            await client.DisconnectAsync(true, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}

