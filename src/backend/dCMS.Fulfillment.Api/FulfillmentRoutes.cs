using System.Text.Json;
using System.Text.Json.Nodes;
using dCMS.AspNetCore.Auth;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Fulfillment.Api.Http;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Fulfillment.Api;

/// <summary>DAI-612: Tenant-scoped fulfillment configuration REST API (eStore parity).</summary>
public static class FulfillmentRoutes
{
    public static void MapFulfillmentRoutes(this WebApplication app, IConfiguration configuration)
    {
        var auth = configuration.IsDcmsAuthEnabled();
        var root = app.MapGroup("/api/v1/tenants/{tenantId}/fulfillment")
            .WithTags("fulfillment")
            .WithTenantAccess(configuration);

        var groupings = root.MapGroup("/groupings");
        Auth(groupings.MapGet("", ListGroupings), auth, false);
        Auth(groupings.MapGet("{groupingId}", GetGrouping), auth, false);
        Auth(groupings.MapPost("", CreateGrouping), auth, true);
        Auth(groupings.MapPut("{groupingId}", UpdateGrouping), auth, true);
        Auth(groupings.MapDelete("{groupingId}", DeleteGrouping), auth, true);

        var slots = root.MapGroup("/groupings/{groupingId}/slots");
        Auth(slots.MapGet("", ListSlots), auth, false);
        Auth(slots.MapGet("{slotId}", GetSlot), auth, false);
        Auth(slots.MapPost("", CreateSlot), auth, true);
        Auth(slots.MapPut("{slotId}", UpdateSlot), auth, true);
        Auth(slots.MapDelete("{slotId}", DeleteSlot), auth, true);

        var coll = root.MapGroup("/collection-locations");
        Auth(coll.MapGet("", ListCollectionLocations), auth, false);
        Auth(coll.MapGet("{locationId}", GetCollectionLocation), auth, false);
        Auth(coll.MapPost("", CreateCollectionLocation), auth, true);
        Auth(coll.MapPut("{locationId}", UpdateCollectionLocation), auth, true);
        Auth(coll.MapDelete("{locationId}", DeleteCollectionLocation), auth, true);

        var partners = root.MapGroup("/logistic-partners");
        Auth(partners.MapGet("", ListLogisticPartners), auth, false);
        Auth(partners.MapGet("{partnerId}", GetLogisticPartner), auth, false);
        Auth(partners.MapPost("", CreateLogisticPartner), auth, true);
        Auth(partners.MapPut("{partnerId}", UpdateLogisticPartner), auth, true);
        Auth(partners.MapDelete("{partnerId}", DeleteLogisticPartner), auth, true);

        Auth(root.MapGet("/settings", GetSettings), auth, false);
        Auth(root.MapPut("/settings", PutSettings), auth, true);
    }

    private static RouteHandlerBuilder Auth(RouteHandlerBuilder b, bool authEnabled, bool write) =>
        authEnabled ? b.RequireAuthorization(write ? DcmsPolicies.CatalogWrite : DcmsPolicies.CatalogRead) : b;

    private static string NormalizeCode(string raw)
    {
        var c = raw.Trim().ToUpperInvariant().Replace('-', '_');
        return c;
    }

    private static bool TryParseDate(string? s, out DateOnly d)
    {
        d = default;
        return !string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s.Trim(), out d);
    }

    private static JsonNode JsonToNode(string? json, string fallback)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? fallback : json) ?? JsonNode.Parse(fallback)!;
        }
        catch (JsonException)
        {
            return JsonNode.Parse(fallback)!;
        }
    }

    private static string[] ParseBrandCodes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(string.IsNullOrWhiteSpace(json) ? "[]" : json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object ToDto(FulfillmentGroupingRow g) => new
    {
        id = g.Id,
        tenantId = g.TenantId,
        groupName = g.GroupName,
        code = g.Code,
        startDate = g.StartDate.ToString("yyyy-MM-dd"),
        endDate = g.EndDate.ToString("yyyy-MM-dd"),
        priority = g.Priority,
        active = g.Active,
        tenantEnabled = g.TenantEnabled,
        maxPerTenant = g.MaxPerTenant,
        deliveryMode = g.DeliveryMode,
        limitSelectedDistributionCenter = g.LimitSelectedDistributionCenter,
        stockLocation = g.StockLocation,
        createdAt = g.CreatedAt,
        updatedAt = g.UpdatedAt,
    };

    private static object ToDto(FulfillmentSlotRow s) => new
    {
        id = s.Id,
        tenantId = s.TenantId,
        groupingId = s.GroupingId,
        name = s.Name,
        code = s.Code,
        mode = s.Mode,
        startingDate = s.StartingDate.ToString("yyyy-MM-dd"),
        endingDate = s.EndingDate.ToString("yyyy-MM-dd"),
        price = s.Price,
        updatedAt = s.UpdatedAt,
    };

    private static object ToDto(CollectionLocationRow r) => new
    {
        id = r.Id,
        tenantId = r.TenantId,
        name = r.Name,
        brandCodes = ParseBrandCodes(r.BrandCodesJson),
        address1 = r.Address1,
        address2 = r.Address2,
        address3 = r.Address3,
        postalCode = r.PostalCode,
        country = r.Country,
        geoLat = r.GeoLat,
        geoLng = r.GeoLng,
        desktopImageSrc = r.DesktopImageSrc,
        desktopImageName = r.DesktopImageName,
        mobileImageSrc = r.MobileImageSrc,
        mobileImageName = r.MobileImageName,
        active = r.Active,
        openingHours = r.OpeningHours,
        closingHours = r.ClosingHours,
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt,
    };

    private static object ToDto(LogisticPartnerRow p) => new
    {
        id = p.Id,
        tenantId = p.TenantId,
        name = p.Name,
        code = p.Code,
        enabled = p.Enabled,
        integratedLogistic = p.IntegratedLogistic,
        createdAt = p.CreatedAt,
        updatedAt = p.UpdatedAt,
    };

    private sealed record GroupingWriteRequest(
        string GroupName,
        string Code,
        string StartDate,
        string EndDate,
        int Priority = 0,
        bool Active = true,
        bool TenantEnabled = true,
        int? MaxPerTenant = null,
        string DeliveryMode = "Local Delivery",
        bool LimitSelectedDistributionCenter = false,
        string? StockLocation = "");

    private sealed record SlotWriteRequest(
        string Name,
        string Code,
        string Mode,
        string StartingDate,
        string EndingDate,
        string Price = "");

    private sealed record CollectionLocationWriteRequest(
        string Name,
        string[]? BrandCodes = null,
        string? Address1 = null,
        string? Address2 = null,
        string? Address3 = null,
        string? PostalCode = null,
        string? Country = null,
        string? GeoLat = null,
        string? GeoLng = null,
        string? DesktopImageSrc = null,
        string? DesktopImageName = null,
        string? MobileImageSrc = null,
        string? MobileImageName = null,
        bool Active = true,
        string? OpeningHours = null,
        string? ClosingHours = null);

    private sealed record LogisticPartnerWriteRequest(
        string Name,
        string Code,
        bool Enabled = true,
        bool IntegratedLogistic = false);

    private sealed record SettingsPutRequest(
        JsonElement? PredefinedFields,
        JsonElement? DynamicFields,
        JsonElement? StockLocations);

    private static string RawOrDefault(JsonElement? el, string fallback)
    {
        if (el is null || el.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return fallback;
        return el.Value.GetRawText();
    }

    private static bool TryNormalizeJson(string raw, out string normalized, out string? error)
    {
        error = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw);
            normalized = doc.RootElement.GetRawText();
            return true;
        }
        catch (JsonException ex)
        {
            normalized = "";
            error = ex.Message;
            return false;
        }
    }

    // ── Groupings ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListGroupings(
        string tenantId, IFulfillmentPersistence db,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (items, total) = await db.ListGroupingsAsync(tenantId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(items.Select(ToDto), new { total, page, pageSize });
    }

    private static async Task<IResult> GetGrouping(
        string tenantId, string groupingId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var g = await db.GetGroupingAsync(groupingId, tenantId, cancellationToken).ConfigureAwait(false);
        return g is null
            ? ApiEnvelope.Error("not_found", "Fulfillment grouping not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(g));
    }

    private static async Task<IResult> CreateGrouping(
        string tenantId, [FromBody] GroupingWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.GroupName))
            return ApiEnvelope.Error("validation_error", "GroupName is required.", StatusCodes.Status400BadRequest);
        var code = NormalizeCode(body.Code);
        if (!FulfillmentGroupingRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!FulfillmentGroupingRow.ValidDeliveryModes.Contains(body.DeliveryMode))
            return ApiEnvelope.Error("validation_error", $"DeliveryMode '{body.DeliveryMode}' is invalid.",
                StatusCodes.Status400BadRequest);
        if (!TryParseDate(body.StartDate, out var sd) || !TryParseDate(body.EndDate, out var ed))
            return ApiEnvelope.Error("validation_error", "StartDate and EndDate must be valid dates (yyyy-MM-dd).",
                StatusCodes.Status400BadRequest);
        if (ed < sd)
            return ApiEnvelope.Error("validation_error", "EndDate must be on or after StartDate.",
                StatusCodes.Status400BadRequest);
        if (await db.GroupingCodeExistsAsync(tenantId, code, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Grouping code '{code}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var id = $"ffg_{Guid.NewGuid():N}";
        var row = new FulfillmentGroupingRow(
            id, tenantId, body.GroupName.Trim(), code, sd, ed,
            body.Priority, body.Active, body.TenantEnabled, body.MaxPerTenant, body.DeliveryMode,
            body.LimitSelectedDistributionCenter, body.StockLocation?.Trim() ?? "", now, now);
        await db.CreateGroupingAsync(row, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { data = ToDto(row), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateGrouping(
        string tenantId, string groupingId, [FromBody] GroupingWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.GetGroupingAsync(groupingId, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", "Fulfillment grouping not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(body.GroupName))
            return ApiEnvelope.Error("validation_error", "GroupName is required.", StatusCodes.Status400BadRequest);
        var code = NormalizeCode(body.Code);
        if (!FulfillmentGroupingRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!FulfillmentGroupingRow.ValidDeliveryModes.Contains(body.DeliveryMode))
            return ApiEnvelope.Error("validation_error", $"DeliveryMode '{body.DeliveryMode}' is invalid.",
                StatusCodes.Status400BadRequest);
        if (!TryParseDate(body.StartDate, out var sd) || !TryParseDate(body.EndDate, out var ed))
            return ApiEnvelope.Error("validation_error", "StartDate and EndDate must be valid dates (yyyy-MM-dd).",
                StatusCodes.Status400BadRequest);
        if (ed < sd)
            return ApiEnvelope.Error("validation_error", "EndDate must be on or after StartDate.",
                StatusCodes.Status400BadRequest);
        if (code != existing.Code &&
            await db.GroupingCodeExistsAsync(tenantId, code, groupingId, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Grouping code '{code}' already exists.", StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var updated = existing with
        {
            GroupName = body.GroupName.Trim(),
            Code = code,
            StartDate = sd,
            EndDate = ed,
            Priority = body.Priority,
            Active = body.Active,
            TenantEnabled = body.TenantEnabled,
            MaxPerTenant = body.MaxPerTenant,
            DeliveryMode = body.DeliveryMode,
            LimitSelectedDistributionCenter = body.LimitSelectedDistributionCenter,
            StockLocation = body.StockLocation?.Trim() ?? "",
            UpdatedAt = now,
        };
        await db.UpdateGroupingAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteGrouping(
        string tenantId, string groupingId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var ok = await db.DeleteGroupingAsync(groupingId, tenantId, cancellationToken).ConfigureAwait(false);
        return ok ? Results.NoContent()
            : ApiEnvelope.Error("not_found", "Fulfillment grouping not found.", StatusCodes.Status404NotFound);
    }

    // ── Slots ─────────────────────────────────────────────────────────────────

    private static async Task<IResult> ListSlots(
        string tenantId, string groupingId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var g = await db.GetGroupingAsync(groupingId, tenantId, cancellationToken).ConfigureAwait(false);
        if (g is null)
            return ApiEnvelope.Error("not_found", "Fulfillment grouping not found.", StatusCodes.Status404NotFound);
        var slots = await db.ListSlotsForGroupingAsync(tenantId, groupingId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(slots.Select(ToDto));
    }

    private static async Task<IResult> GetSlot(
        string tenantId, string groupingId, string slotId, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var s = await db.GetSlotAsync(slotId, tenantId, cancellationToken).ConfigureAwait(false);
        if (s is null || s.GroupingId != groupingId)
            return ApiEnvelope.Error("not_found", "Fulfillment slot not found.", StatusCodes.Status404NotFound);
        return ApiEnvelope.Ok(ToDto(s));
    }

    private static async Task<IResult> CreateSlot(
        string tenantId, string groupingId, [FromBody] SlotWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var g = await db.GetGroupingAsync(groupingId, tenantId, cancellationToken).ConfigureAwait(false);
        if (g is null)
            return ApiEnvelope.Error("not_found", "Fulfillment grouping not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);
        var code = NormalizeCode(body.Code);
        if (!FulfillmentSlotRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!FulfillmentGroupingRow.ValidDeliveryModes.Contains(body.Mode))
            return ApiEnvelope.Error("validation_error", $"Mode '{body.Mode}' is invalid.", StatusCodes.Status400BadRequest);
        if (!TryParseDate(body.StartingDate, out var sd) || !TryParseDate(body.EndingDate, out var ed))
            return ApiEnvelope.Error("validation_error", "StartingDate and EndingDate must be valid dates.",
                StatusCodes.Status400BadRequest);
        if (ed < sd)
            return ApiEnvelope.Error("validation_error", "EndingDate must be on or after StartingDate.",
                StatusCodes.Status400BadRequest);
        if (await db.SlotCodeExistsAsync(tenantId, groupingId, code, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Slot code '{code}' already exists in this grouping.",
                StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var id = $"ffs_{Guid.NewGuid():N}";
        var row = new FulfillmentSlotRow(id, tenantId, groupingId, body.Name.Trim(), code, body.Mode, sd, ed,
            body.Price?.Trim() ?? "", now);
        await db.CreateSlotAsync(row, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { data = ToDto(row), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateSlot(
        string tenantId, string groupingId, string slotId, [FromBody] SlotWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.GetSlotAsync(slotId, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.GroupingId != groupingId)
            return ApiEnvelope.Error("not_found", "Fulfillment slot not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);
        var code = NormalizeCode(body.Code);
        if (!FulfillmentSlotRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (!FulfillmentGroupingRow.ValidDeliveryModes.Contains(body.Mode))
            return ApiEnvelope.Error("validation_error", $"Mode '{body.Mode}' is invalid.", StatusCodes.Status400BadRequest);
        if (!TryParseDate(body.StartingDate, out var sd) || !TryParseDate(body.EndingDate, out var ed))
            return ApiEnvelope.Error("validation_error", "StartingDate and EndingDate must be valid dates.",
                StatusCodes.Status400BadRequest);
        if (ed < sd)
            return ApiEnvelope.Error("validation_error", "EndingDate must be on or after StartingDate.",
                StatusCodes.Status400BadRequest);
        if (code != existing.Code &&
            await db.SlotCodeExistsAsync(tenantId, groupingId, code, slotId, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Slot code '{code}' already exists in this grouping.",
                StatusCodes.Status409Conflict);

        var now = DateTimeOffset.UtcNow;
        var updated = existing with
        {
            Name = body.Name.Trim(),
            Code = code,
            Mode = body.Mode,
            StartingDate = sd,
            EndingDate = ed,
            Price = body.Price?.Trim() ?? "",
            UpdatedAt = now,
        };
        await db.UpdateSlotAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteSlot(
        string tenantId, string groupingId, string slotId, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.GetSlotAsync(slotId, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.GroupingId != groupingId)
            return ApiEnvelope.Error("not_found", "Fulfillment slot not found.", StatusCodes.Status404NotFound);
        await db.DeleteSlotAsync(slotId, tenantId, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // ── Collection locations ──────────────────────────────────────────────────

    private static async Task<IResult> ListCollectionLocations(
        string tenantId, IFulfillmentPersistence db,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (items, total) = await db.ListCollectionLocationsAsync(tenantId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(items.Select(ToDto), new { total, page, pageSize });
    }

    private static async Task<IResult> GetCollectionLocation(
        string tenantId, string locationId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var r = await db.GetCollectionLocationAsync(locationId, tenantId, cancellationToken).ConfigureAwait(false);
        return r is null
            ? ApiEnvelope.Error("not_found", "Collection location not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(r));
    }

    private static async Task<IResult> CreateCollectionLocation(
        string tenantId, [FromBody] CollectionLocationWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);
        var now = DateTimeOffset.UtcNow;
        var id = $"ffc_{Guid.NewGuid():N}";
        var brandsJson = JsonSerializer.Serialize(body.BrandCodes ?? []);
        var row = new CollectionLocationRow(
            id, tenantId, body.Name.Trim(), brandsJson,
            body.Address1, body.Address2, body.Address3, body.PostalCode, body.Country,
            body.GeoLat, body.GeoLng, body.DesktopImageSrc, body.DesktopImageName,
            body.MobileImageSrc, body.MobileImageName, body.Active, body.OpeningHours, body.ClosingHours, now, now);
        await db.CreateCollectionLocationAsync(row, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { data = ToDto(row), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateCollectionLocation(
        string tenantId, string locationId, [FromBody] CollectionLocationWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.GetCollectionLocationAsync(locationId, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", "Collection location not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);
        var now = DateTimeOffset.UtcNow;
        var brandsJson = JsonSerializer.Serialize(body.BrandCodes ?? []);
        var updated = existing with
        {
            Name = body.Name.Trim(),
            BrandCodesJson = brandsJson,
            Address1 = body.Address1,
            Address2 = body.Address2,
            Address3 = body.Address3,
            PostalCode = body.PostalCode,
            Country = body.Country,
            GeoLat = body.GeoLat,
            GeoLng = body.GeoLng,
            DesktopImageSrc = body.DesktopImageSrc,
            DesktopImageName = body.DesktopImageName,
            MobileImageSrc = body.MobileImageSrc,
            MobileImageName = body.MobileImageName,
            Active = body.Active,
            OpeningHours = body.OpeningHours,
            ClosingHours = body.ClosingHours,
            UpdatedAt = now,
        };
        await db.UpdateCollectionLocationAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteCollectionLocation(
        string tenantId, string locationId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var ok = await db.DeleteCollectionLocationAsync(locationId, tenantId, cancellationToken).ConfigureAwait(false);
        return ok ? Results.NoContent()
            : ApiEnvelope.Error("not_found", "Collection location not found.", StatusCodes.Status404NotFound);
    }

    // ── Logistic partners ───────────────────────────────────────────────────────

    private static async Task<IResult> ListLogisticPartners(
        string tenantId, IFulfillmentPersistence db,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (items, total) = await db.ListLogisticPartnersAsync(tenantId, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return ApiEnvelope.Ok(items.Select(ToDto), new { total, page, pageSize });
    }

    private static async Task<IResult> GetLogisticPartner(
        string tenantId, string partnerId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var p = await db.GetLogisticPartnerAsync(partnerId, tenantId, cancellationToken).ConfigureAwait(false);
        return p is null
            ? ApiEnvelope.Error("not_found", "Logistic partner not found.", StatusCodes.Status404NotFound)
            : ApiEnvelope.Ok(ToDto(p));
    }

    private static async Task<IResult> CreateLogisticPartner(
        string tenantId, [FromBody] LogisticPartnerWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);
        var code = NormalizeCode(body.Code);
        if (!LogisticPartnerRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (await db.LogisticPartnerCodeExistsAsync(tenantId, code, null, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Partner code '{code}' already exists.", StatusCodes.Status409Conflict);
        var now = DateTimeOffset.UtcNow;
        var id = $"ffl_{Guid.NewGuid():N}";
        var row = new LogisticPartnerRow(id, tenantId, body.Name.Trim(), code, body.Enabled, body.IntegratedLogistic,
            now, now);
        await db.CreateLogisticPartnerAsync(row, cancellationToken).ConfigureAwait(false);
        return Results.Json(new { data = ToDto(row), meta = (object?)null, error = (object?)null },
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateLogisticPartner(
        string tenantId, string partnerId, [FromBody] LogisticPartnerWriteRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.GetLogisticPartnerAsync(partnerId, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return ApiEnvelope.Error("not_found", "Logistic partner not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(body.Name))
            return ApiEnvelope.Error("validation_error", "Name is required.", StatusCodes.Status400BadRequest);
        var code = NormalizeCode(body.Code);
        if (!LogisticPartnerRow.IsValidCode(code))
            return ApiEnvelope.Error("validation_error", $"Code '{code}' is invalid.", StatusCodes.Status400BadRequest);
        if (code != existing.Code &&
            await db.LogisticPartnerCodeExistsAsync(tenantId, code, partnerId, cancellationToken).ConfigureAwait(false))
            return ApiEnvelope.Error("conflict", $"Partner code '{code}' already exists.", StatusCodes.Status409Conflict);
        var now = DateTimeOffset.UtcNow;
        var updated = existing with
        {
            Name = body.Name.Trim(),
            Code = code,
            Enabled = body.Enabled,
            IntegratedLogistic = body.IntegratedLogistic,
            UpdatedAt = now,
        };
        await db.UpdateLogisticPartnerAsync(updated, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(ToDto(updated));
    }

    private static async Task<IResult> DeleteLogisticPartner(
        string tenantId, string partnerId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var ok = await db.DeleteLogisticPartnerAsync(partnerId, tenantId, cancellationToken).ConfigureAwait(false);
        return ok ? Results.NoContent()
            : ApiEnvelope.Error("not_found", "Logistic partner not found.", StatusCodes.Status404NotFound);
    }

    // ── Settings ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSettings(
        string tenantId, IFulfillmentPersistence db, CancellationToken cancellationToken = default)
    {
        var row = await db.GetTenantSettingsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new
        {
            predefinedFields = JsonToNode(row?.PredefinedFieldsJson, "[]"),
            dynamicFields = JsonToNode(row?.DynamicFieldsJson, "[]"),
            stockLocations = JsonToNode(row?.StockLocationsJson, "[]"),
            updatedAt = row?.UpdatedAt,
        });
    }

    private static async Task<IResult> PutSettings(
        string tenantId, [FromBody] SettingsPutRequest body, IFulfillmentPersistence db,
        CancellationToken cancellationToken = default)
    {
        var pre = RawOrDefault(body.PredefinedFields, "[]");
        var dyn = RawOrDefault(body.DynamicFields, "[]");
        var stock = RawOrDefault(body.StockLocations, "[]");
        if (!TryNormalizeJson(pre, out var preN, out var errPre))
            return ApiEnvelope.Error("validation_error", $"predefinedFields: {errPre}", StatusCodes.Status400BadRequest);
        if (!TryNormalizeJson(dyn, out var dynN, out var errDyn))
            return ApiEnvelope.Error("validation_error", $"dynamicFields: {errDyn}", StatusCodes.Status400BadRequest);
        if (!TryNormalizeJson(stock, out var stockN, out var errStock))
            return ApiEnvelope.Error("validation_error", $"stockLocations: {errStock}", StatusCodes.Status400BadRequest);
        var now = DateTimeOffset.UtcNow;
        var row = new FulfillmentTenantSettingsRow(tenantId, preN, dynN, stockN, now);
        await db.UpsertTenantSettingsAsync(row, cancellationToken).ConfigureAwait(false);
        return ApiEnvelope.Ok(new
        {
            predefinedFields = JsonToNode(preN, "[]"),
            dynamicFields = JsonToNode(dynN, "[]"),
            stockLocations = JsonToNode(stockN, "[]"),
            updatedAt = now,
        });
    }
}
