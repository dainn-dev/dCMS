using System.Data;
using Dapper;

namespace dCMS.Infrastructure.Persistence;

/// <summary>
/// Dapper type handler that converts the <see cref="DateTime"/> Npgsql returns for
/// <c>timestamptz</c>/<c>timestamp</c> columns into <see cref="DateTimeOffset"/>.
///
/// Without this, Dapper cannot materialize <b>positional records</b> (constructor-only)
/// whose parameters are <see cref="DateTimeOffset"/>: the constructor-matching step rejects
/// the column because <c>DateTime</c> is not assignable to <c>DateTimeOffset</c>, throwing
/// <c>InvalidOperationException: ... one matching signature ... is required for ... materialization</c>.
/// Registering this handler makes <c>DateTimeOffset</c> (and <c>DateTimeOffset?</c>) bindable
/// everywhere, including positional records of any size.
/// </summary>
public sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset dto => dto,
        // Npgsql returns timestamptz as DateTime with Kind=Utc; timestamp as Unspecified.
        DateTime dt => dt.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(dt, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero),
            _ => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
        },
        _ => throw new InvalidCastException($"Cannot convert {value?.GetType().FullName ?? "null"} to DateTimeOffset."),
    };

    // Writes are unchanged from Dapper's default: pass the DateTimeOffset through; Npgsql
    // maps it to timestamptz exactly as before.
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value;
}

/// <summary>Centralised registration of dCMS Dapper type handlers. Idempotent and thread-safe.</summary>
public static class DapperTypeHandlers
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        // Registering the value-type handler also covers Nullable<DateTimeOffset>.
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }
}
