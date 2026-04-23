using Dapper;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Npgsql;

namespace dCMS.Infrastructure.Catalog;

/// <summary>Dapper + PostgreSQL for fulfillment configuration (DAI-612).</summary>
public sealed class SqlFulfillmentPersistence(string connectionString) : IFulfillmentPersistence
{
    private readonly string _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private static DateOnly ToDateOnly(DateTime d) => DateOnly.FromDateTime(DateTime.SpecifyKind(d, DateTimeKind.Utc));

    private sealed class GroupingDapperRow
    {
        public string   Id                               { get; init; } = null!;
        public string   TenantId                         { get; init; } = null!;
        public string   GroupName                        { get; init; } = null!;
        public string   Code                             { get; init; } = null!;
        public DateTime StartDate                        { get; init; }
        public DateTime EndDate                          { get; init; }
        public int      Priority                         { get; init; }
        public bool     Active                           { get; init; }
        public bool     TenantEnabled                    { get; init; }
        public int?     MaxPerTenant                     { get; init; }
        public string   DeliveryMode                     { get; init; } = null!;
        public bool     LimitSelectedDistributionCenter { get; init; }
        public string   StockLocation                    { get; init; } = "";
        public DateTime CreatedAt                        { get; init; }
        public DateTime UpdatedAt                        { get; init; }

        public FulfillmentGroupingRow ToModel() => new(
            Id, TenantId, GroupName, Code,
            ToDateOnly(StartDate), ToDateOnly(EndDate),
            Priority, Active, TenantEnabled, MaxPerTenant, DeliveryMode,
            LimitSelectedDistributionCenter, StockLocation,
            new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
            new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc), TimeSpan.Zero));
    }

    private const string GroupingCols = """
        "Id","TenantId","GroupName","Code","StartDate","EndDate","Priority","Active","TenantEnabled",
        "MaxPerTenant","DeliveryMode","LimitSelectedDistributionCenter","StockLocation","CreatedAt","UpdatedAt"
        """;

    private sealed class SlotDapperRow
    {
        public string   Id           { get; init; } = null!;
        public string   TenantId     { get; init; } = null!;
        public string   GroupingId   { get; init; } = null!;
        public string   Name         { get; init; } = null!;
        public string   Code         { get; init; } = null!;
        public string   Mode         { get; init; } = null!;
        public DateTime StartingDate { get; init; }
        public DateTime EndingDate   { get; init; }
        public string   Price        { get; init; } = "";
        public DateTime UpdatedAt    { get; init; }

        public FulfillmentSlotRow ToModel() => new(
            Id, TenantId, GroupingId, Name, Code, Mode,
            ToDateOnly(StartingDate), ToDateOnly(EndingDate), Price,
            new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc), TimeSpan.Zero));
    }

    private const string SlotCols = """
        "Id","TenantId","GroupingId","Name","Code","Mode","StartingDate","EndingDate","Price","UpdatedAt"
        """;

    private sealed class CollDapperRow
    {
        public string   Id               { get; init; } = null!;
        public string   TenantId         { get; init; } = null!;
        public string   Name             { get; init; } = null!;
        public string   BrandCodesJson   { get; init; } = "[]";
        public string?  Address1         { get; init; }
        public string?  Address2         { get; init; }
        public string?  Address3         { get; init; }
        public string?  PostalCode       { get; init; }
        public string?  Country          { get; init; }
        public string?  GeoLat           { get; init; }
        public string?  GeoLng           { get; init; }
        public string?  DesktopImageSrc  { get; init; }
        public string?  DesktopImageName { get; init; }
        public string?  MobileImageSrc   { get; init; }
        public string?  MobileImageName  { get; init; }
        public bool     Active           { get; init; }
        public string?  OpeningHours     { get; init; }
        public string?  ClosingHours     { get; init; }
        public DateTime CreatedAt        { get; init; }
        public DateTime UpdatedAt        { get; init; }

        public CollectionLocationRow ToModel() => new(
            Id, TenantId, Name, BrandCodesJson,
            Address1, Address2, Address3, PostalCode, Country, GeoLat, GeoLng,
            DesktopImageSrc, DesktopImageName, MobileImageSrc, MobileImageName,
            Active, OpeningHours, ClosingHours,
            new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
            new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc), TimeSpan.Zero));
    }

    private const string CollCols = """
        "Id","TenantId","Name","BrandCodesJson","Address1","Address2","Address3","PostalCode","Country",
        "GeoLat","GeoLng","DesktopImageSrc","DesktopImageName","MobileImageSrc","MobileImageName",
        "Active","OpeningHours","ClosingHours","CreatedAt","UpdatedAt"
        """;

    private sealed class LpDapperRow
    {
        public string   Id                   { get; init; } = null!;
        public string   TenantId             { get; init; } = null!;
        public string   Name                 { get; init; } = null!;
        public string   Code                 { get; init; } = null!;
        public bool     Enabled              { get; init; }
        public bool     IntegratedLogistic   { get; init; }
        public DateTime CreatedAt            { get; init; }
        public DateTime UpdatedAt            { get; init; }

        public LogisticPartnerRow ToModel() => new(
            Id, TenantId, Name, Code, Enabled, IntegratedLogistic,
            new DateTimeOffset(DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
            new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc), TimeSpan.Zero));
    }

    private const string LpCols = """
        "Id","TenantId","Name","Code","Enabled","IntegratedLogistic","CreatedAt","UpdatedAt"
        """;

    private sealed class SettingsDapperRow
    {
        public string   TenantId             { get; init; } = null!;
        public string   PredefinedFieldsJson { get; init; } = "[]";
        public string   DynamicFieldsJson    { get; init; } = "[]";
        public string   StockLocationsJson   { get; init; } = "[]";
        public DateTime UpdatedAt            { get; init; }

        public FulfillmentTenantSettingsRow ToModel() => new(
            TenantId, PredefinedFieldsJson, DynamicFieldsJson, StockLocationsJson,
            new DateTimeOffset(DateTime.SpecifyKind(UpdatedAt, DateTimeKind.Utc), TimeSpan.Zero));
    }

    public async Task<(IReadOnlyList<FulfillmentGroupingRow> Items, int Total)> ListGroupingsAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        await using var conn = new NpgsqlConnection(_cs);
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("""SELECT COUNT(*)::INT FROM "FulfillmentGroupings" WHERE "TenantId" = @TenantId""",
                p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        p.Add("PageSize", pageSize);
        p.Add("Offset", (page - 1) * pageSize);
        var rows = await conn.QueryAsync<GroupingDapperRow>(
            new CommandDefinition($"""
                SELECT {GroupingCols} FROM "FulfillmentGroupings"
                WHERE "TenantId" = @TenantId
                ORDER BY "Priority" ASC, "UpdatedAt" DESC
                LIMIT @PageSize OFFSET @Offset
                """, p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<FulfillmentGroupingRow?> GetGroupingAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<GroupingDapperRow>(
            new CommandDefinition($"""
                SELECT {GroupingCols} FROM "FulfillmentGroupings"
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<bool> GroupingCodeExistsAsync(string tenantId, string code, string? exceptId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var sql = exceptId is null
            ? """SELECT EXISTS(SELECT 1 FROM "FulfillmentGroupings" WHERE "TenantId" = @TenantId AND "Code" = @Code)"""
            : """SELECT EXISTS(SELECT 1 FROM "FulfillmentGroupings" WHERE "TenantId" = @TenantId AND "Code" = @Code AND "Id" <> @ExceptId)""";
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code, ExceptId = exceptId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CreateGroupingAsync(FulfillmentGroupingRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(
            new CommandDefinition("""
                INSERT INTO "FulfillmentGroupings"
                ("Id","TenantId","GroupName","Code","StartDate","EndDate","Priority","Active","TenantEnabled",
                 "MaxPerTenant","DeliveryMode","LimitSelectedDistributionCenter","StockLocation","CreatedAt","UpdatedAt")
                VALUES (@Id,@TenantId,@GroupName,@Code,@StartDate,@EndDate,@Priority,@Active,@TenantEnabled,
                        @MaxPerTenant,@DeliveryMode,@LimitSelectedDistributionCenter,@StockLocation,@CreatedAt,@UpdatedAt)
                """, new
            {
                row.Id,
                row.TenantId,
                row.GroupName,
                row.Code,
                StartDate = row.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                EndDate   = row.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                row.Priority,
                row.Active,
                row.TenantEnabled,
                row.MaxPerTenant,
                row.DeliveryMode,
                row.LimitSelectedDistributionCenter,
                row.StockLocation,
                CreatedAt = row.CreatedAt.UtcDateTime,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateGroupingAsync(FulfillmentGroupingRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""
                UPDATE "FulfillmentGroupings" SET
                    "GroupName" = @GroupName, "Code" = @Code,
                    "StartDate" = @StartDate, "EndDate" = @EndDate,
                    "Priority" = @Priority, "Active" = @Active, "TenantEnabled" = @TenantEnabled,
                    "MaxPerTenant" = @MaxPerTenant, "DeliveryMode" = @DeliveryMode,
                    "LimitSelectedDistributionCenter" = @LimitSelectedDistributionCenter,
                    "StockLocation" = @StockLocation, "UpdatedAt" = @UpdatedAt
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new
            {
                row.Id,
                row.TenantId,
                row.GroupName,
                row.Code,
                StartDate = row.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                EndDate   = row.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                row.Priority,
                row.Active,
                row.TenantEnabled,
                row.MaxPerTenant,
                row.DeliveryMode,
                row.LimitSelectedDistributionCenter,
                row.StockLocation,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteGroupingAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""DELETE FROM "FulfillmentGroupings" WHERE "Id" = @Id AND "TenantId" = @TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<IReadOnlyList<FulfillmentSlotRow>> ListSlotsForGroupingAsync(string tenantId, string groupingId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var rows = await conn.QueryAsync<SlotDapperRow>(
            new CommandDefinition($"""
                SELECT {SlotCols} FROM "FulfillmentSlots"
                WHERE "TenantId" = @TenantId AND "GroupingId" = @GroupingId
                ORDER BY "Code" ASC
                """, new { TenantId = tenantId, GroupingId = groupingId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<FulfillmentSlotRow?> GetSlotAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<SlotDapperRow>(
            new CommandDefinition($"""
                SELECT {SlotCols} FROM "FulfillmentSlots"
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<bool> SlotCodeExistsAsync(string tenantId, string groupingId, string code, string? exceptId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var sql = exceptId is null
            ? """SELECT EXISTS(SELECT 1 FROM "FulfillmentSlots" WHERE "TenantId" = @TenantId AND "GroupingId" = @GroupingId AND "Code" = @Code)"""
            : """SELECT EXISTS(SELECT 1 FROM "FulfillmentSlots" WHERE "TenantId" = @TenantId AND "GroupingId" = @GroupingId AND "Code" = @Code AND "Id" <> @ExceptId)""";
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql,
                new { TenantId = tenantId, GroupingId = groupingId, Code = code, ExceptId = exceptId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CreateSlotAsync(FulfillmentSlotRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(
            new CommandDefinition("""
                INSERT INTO "FulfillmentSlots"
                ("Id","TenantId","GroupingId","Name","Code","Mode","StartingDate","EndingDate","Price","UpdatedAt")
                VALUES (@Id,@TenantId,@GroupingId,@Name,@Code,@Mode,@StartingDate,@EndingDate,@Price,@UpdatedAt)
                """, new
            {
                row.Id,
                row.TenantId,
                row.GroupingId,
                row.Name,
                row.Code,
                row.Mode,
                StartingDate = row.StartingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                EndingDate   = row.EndingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                row.Price,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateSlotAsync(FulfillmentSlotRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""
                UPDATE "FulfillmentSlots" SET
                    "Name" = @Name, "Code" = @Code, "Mode" = @Mode,
                    "StartingDate" = @StartingDate, "EndingDate" = @EndingDate,
                    "Price" = @Price, "UpdatedAt" = @UpdatedAt
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new
            {
                row.Id,
                row.TenantId,
                row.Name,
                row.Code,
                row.Mode,
                StartingDate = row.StartingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                EndingDate   = row.EndingDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                row.Price,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteSlotAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""DELETE FROM "FulfillmentSlots" WHERE "Id" = @Id AND "TenantId" = @TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<(IReadOnlyList<CollectionLocationRow> Items, int Total)> ListCollectionLocationsAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        await using var conn = new NpgsqlConnection(_cs);
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("""SELECT COUNT(*)::INT FROM "CollectionLocations" WHERE "TenantId" = @TenantId""",
                p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        p.Add("PageSize", pageSize);
        p.Add("Offset", (page - 1) * pageSize);
        var rows = await conn.QueryAsync<CollDapperRow>(
            new CommandDefinition($"""
                SELECT {CollCols} FROM "CollectionLocations"
                WHERE "TenantId" = @TenantId
                ORDER BY "Name" ASC
                LIMIT @PageSize OFFSET @Offset
                """, p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<CollectionLocationRow?> GetCollectionLocationAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<CollDapperRow>(
            new CommandDefinition($"""
                SELECT {CollCols} FROM "CollectionLocations"
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task CreateCollectionLocationAsync(CollectionLocationRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(
            new CommandDefinition("""
                INSERT INTO "CollectionLocations"
                ("Id","TenantId","Name","BrandCodesJson","Address1","Address2","Address3","PostalCode","Country",
                 "GeoLat","GeoLng","DesktopImageSrc","DesktopImageName","MobileImageSrc","MobileImageName",
                 "Active","OpeningHours","ClosingHours","CreatedAt","UpdatedAt")
                VALUES (@Id,@TenantId,@Name,@BrandCodesJson,@Address1,@Address2,@Address3,@PostalCode,@Country,
                        @GeoLat,@GeoLng,@DesktopImageSrc,@DesktopImageName,@MobileImageSrc,@MobileImageName,
                        @Active,@OpeningHours,@ClosingHours,@CreatedAt,@UpdatedAt)
                """, new
            {
                row.Id,
                row.TenantId,
                row.Name,
                row.BrandCodesJson,
                row.Address1,
                row.Address2,
                row.Address3,
                row.PostalCode,
                row.Country,
                row.GeoLat,
                row.GeoLng,
                row.DesktopImageSrc,
                row.DesktopImageName,
                row.MobileImageSrc,
                row.MobileImageName,
                row.Active,
                row.OpeningHours,
                row.ClosingHours,
                CreatedAt = row.CreatedAt.UtcDateTime,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateCollectionLocationAsync(CollectionLocationRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""
                UPDATE "CollectionLocations" SET
                    "Name" = @Name, "BrandCodesJson" = @BrandCodesJson,
                    "Address1" = @Address1, "Address2" = @Address2, "Address3" = @Address3,
                    "PostalCode" = @PostalCode, "Country" = @Country,
                    "GeoLat" = @GeoLat, "GeoLng" = @GeoLng,
                    "DesktopImageSrc" = @DesktopImageSrc, "DesktopImageName" = @DesktopImageName,
                    "MobileImageSrc" = @MobileImageSrc, "MobileImageName" = @MobileImageName,
                    "Active" = @Active, "OpeningHours" = @OpeningHours, "ClosingHours" = @ClosingHours,
                    "UpdatedAt" = @UpdatedAt
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new
            {
                row.Id,
                row.TenantId,
                row.Name,
                row.BrandCodesJson,
                row.Address1,
                row.Address2,
                row.Address3,
                row.PostalCode,
                row.Country,
                row.GeoLat,
                row.GeoLng,
                row.DesktopImageSrc,
                row.DesktopImageName,
                row.MobileImageSrc,
                row.MobileImageName,
                row.Active,
                row.OpeningHours,
                row.ClosingHours,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteCollectionLocationAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""DELETE FROM "CollectionLocations" WHERE "Id" = @Id AND "TenantId" = @TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<(IReadOnlyList<LogisticPartnerRow> Items, int Total)> ListLogisticPartnersAsync(
        string tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);
        await using var conn = new NpgsqlConnection(_cs);
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var total = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("""SELECT COUNT(*)::INT FROM "LogisticPartners" WHERE "TenantId" = @TenantId""",
                p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        p.Add("PageSize", pageSize);
        p.Add("Offset", (page - 1) * pageSize);
        var rows = await conn.QueryAsync<LpDapperRow>(
            new CommandDefinition($"""
                SELECT {LpCols} FROM "LogisticPartners"
                WHERE "TenantId" = @TenantId
                ORDER BY "Name" ASC
                LIMIT @PageSize OFFSET @Offset
                """, p, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return (rows.Select(r => r.ToModel()).ToList(), total);
    }

    public async Task<LogisticPartnerRow?> GetLogisticPartnerAsync(string id, string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<LpDapperRow>(
            new CommandDefinition($"""
                SELECT {LpCols} FROM "LogisticPartners"
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<bool> LogisticPartnerCodeExistsAsync(string tenantId, string code, string? exceptId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var sql = exceptId is null
            ? """SELECT EXISTS(SELECT 1 FROM "LogisticPartners" WHERE "TenantId" = @TenantId AND "Code" = @Code)"""
            : """SELECT EXISTS(SELECT 1 FROM "LogisticPartners" WHERE "TenantId" = @TenantId AND "Code" = @Code AND "Id" <> @ExceptId)""";
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { TenantId = tenantId, Code = code, ExceptId = exceptId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CreateLogisticPartnerAsync(LogisticPartnerRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(
            new CommandDefinition("""
                INSERT INTO "LogisticPartners"
                ("Id","TenantId","Name","Code","Enabled","IntegratedLogistic","CreatedAt","UpdatedAt")
                VALUES (@Id,@TenantId,@Name,@Code,@Enabled,@IntegratedLogistic,@CreatedAt,@UpdatedAt)
                """, new
            {
                row.Id,
                row.TenantId,
                row.Name,
                row.Code,
                row.Enabled,
                row.IntegratedLogistic,
                CreatedAt = row.CreatedAt.UtcDateTime,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateLogisticPartnerAsync(LogisticPartnerRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""
                UPDATE "LogisticPartners" SET
                    "Name" = @Name, "Code" = @Code, "Enabled" = @Enabled,
                    "IntegratedLogistic" = @IntegratedLogistic, "UpdatedAt" = @UpdatedAt
                WHERE "Id" = @Id AND "TenantId" = @TenantId
                """, new
            {
                row.Id,
                row.TenantId,
                row.Name,
                row.Code,
                row.Enabled,
                row.IntegratedLogistic,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<bool> DeleteLogisticPartnerAsync(string id, string tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var n = await conn.ExecuteAsync(
            new CommandDefinition("""DELETE FROM "LogisticPartners" WHERE "Id" = @Id AND "TenantId" = @TenantId""",
                new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return n > 0;
    }

    public async Task<FulfillmentTenantSettingsRow?> GetTenantSettingsAsync(string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        var row = await conn.QuerySingleOrDefaultAsync<SettingsDapperRow>(
            new CommandDefinition("""
                SELECT "TenantId","PredefinedFieldsJson","DynamicFieldsJson","StockLocationsJson","UpdatedAt"
                FROM "FulfillmentTenantSettings"
                WHERE "TenantId" = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task UpsertTenantSettingsAsync(FulfillmentTenantSettingsRow row, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.ExecuteAsync(
            new CommandDefinition("""
                INSERT INTO "FulfillmentTenantSettings"
                ("TenantId","PredefinedFieldsJson","DynamicFieldsJson","StockLocationsJson","UpdatedAt")
                VALUES (@TenantId,@PredefinedFieldsJson,@DynamicFieldsJson,@StockLocationsJson,@UpdatedAt)
                ON CONFLICT ("TenantId") DO UPDATE SET
                    "PredefinedFieldsJson" = EXCLUDED."PredefinedFieldsJson",
                    "DynamicFieldsJson" = EXCLUDED."DynamicFieldsJson",
                    "StockLocationsJson" = EXCLUDED."StockLocationsJson",
                    "UpdatedAt" = EXCLUDED."UpdatedAt"
                """, new
            {
                row.TenantId,
                row.PredefinedFieldsJson,
                row.DynamicFieldsJson,
                row.StockLocationsJson,
                UpdatedAt = row.UpdatedAt.UtcDateTime,
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
