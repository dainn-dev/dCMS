using System.Globalization;
using Dapper;
using MassTransit;
using Npgsql;

namespace dCMS.Catalog.Worker.Imports.Processors;

// DAI-708 — promo codes bulk import row processor.
// Required: code, campaign_id, discount_type, discount_value.
// Optional: valid_from, valid_to, name (used as NameJson default), min_spend.
// Validates the campaign exists in tenant; only seeds promos when the
// campaign workflow state is approved (per DAI-684 brief).
public sealed class PromoCodeRowProcessor : IImportRowProcessor
{
    private readonly string _connectionString;
    public PromoCodeRowProcessor(string catalogConnectionString) { _connectionString = catalogConnectionString; }

    public string Type => "promo-codes";

    public async Task<RowResult> ProcessAsync(ImportRow row, ImportContext ctx, CancellationToken ct)
    {
        if (!row.Cells.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            return RowResult.Err("code is required");
        if (!row.Cells.TryGetValue("campaign_id", out var campaignId) || string.IsNullOrWhiteSpace(campaignId))
            return RowResult.Err("campaign_id is required");
        if (!row.Cells.TryGetValue("discount_type", out var dtype) || string.IsNullOrWhiteSpace(dtype))
            return RowResult.Err("discount_type is required");
        if (!row.Cells.TryGetValue("discount_value", out var dval) || string.IsNullOrWhiteSpace(dval))
            return RowResult.Err("discount_value is required");

        DateTimeOffset? validFrom = ParseDate(row.Cells.GetValueOrDefault("valid_from"));
        DateTimeOffset? validTo = ParseDate(row.Cells.GetValueOrDefault("valid_to"));
        var minSpend = row.Cells.GetValueOrDefault("min_spend") ?? string.Empty;

        await using var cn = new NpgsqlConnection(_connectionString);
        await cn.OpenAsync(ct).ConfigureAwait(false);

        var state = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT \"WorkflowState\" FROM \"Campaigns\" WHERE \"Id\" = @id AND \"TenantId\" = @t LIMIT 1",
            new { id = campaignId, t = ctx.TenantId }, cancellationToken: ct)).ConfigureAwait(false);
        if (state is null)
            return RowResult.Err($"campaign {campaignId} not found");
        if (!string.Equals(state, "approved", StringComparison.OrdinalIgnoreCase))
            return RowResult.Err($"campaign {campaignId} is not approved (state={state})");

        var existingId = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT \"Id\" FROM \"PromoCodes\" WHERE \"TenantId\" = @t AND \"Code\" = @c LIMIT 1",
            new { t = ctx.TenantId, c = code }, cancellationToken: ct)).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        if (existingId is not null)
        {
            await cn.ExecuteAsync(new CommandDefinition(@"
                UPDATE ""PromoCodes""
                   SET ""DiscountType""  = @dtype,
                       ""DiscountValue"" = @dval,
                       ""StartDate""     = @from,
                       ""EndDate""       = @to,
                       ""MinSpend""      = @ms,
                       ""UpdatedAt""     = @now
                 WHERE ""Id"" = @id",
                new { id = existingId, dtype, dval, from = validFrom, to = validTo, ms = minSpend, now },
                cancellationToken: ct)).ConfigureAwait(false);
            return RowResult.Ok;
        }

        var promoId = "promo_" + NewId.NextSequentialGuid().ToString("N")[..20];
        await cn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO ""PromoCodes""
                (""Id"", ""TenantId"", ""Code"", ""NameJson"", ""DiscountType"", ""DiscountValue"",
                 ""WorkflowState"", ""MinSpend"", ""StartDate"", ""EndDate"", ""CreatedAt"", ""UpdatedAt"")
            VALUES
                (@id, @t, @c, '{}', @dtype, @dval, 'draft', @ms, @from, @to, @now, @now)",
            new { id = promoId, t = ctx.TenantId, c = code, dtype, dval, ms = minSpend, from = validFrom, to = validTo, now },
            cancellationToken: ct)).ConfigureAwait(false);

        return RowResult.Ok;
    }

    private static DateTimeOffset? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;
        return null;
    }
}
