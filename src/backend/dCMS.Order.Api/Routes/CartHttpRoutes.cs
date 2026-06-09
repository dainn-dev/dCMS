using System.Security.Claims;
using System.Text.Json;
using dCMS.AspNetCore.Auth;
using dCMS.Billing.Domain;
using dCMS.Core.Exceptions;
using dCMS.Core.Services;
using dCMS.Order.Api.Contracts;
using dCMS.Order.Api.Security;
using dCMS.Order.Core.Cart;
using dCMS.Order.Core.Ordering;
using dCMS.Order.Core.Integration;
using OutOfStockException = dCMS.Order.Core.Integration.OutOfStockException;
using Microsoft.AspNetCore.Mvc;

namespace dCMS.Order.Api.Routes;

public static class CartHttpRoutes
{
  private static readonly JsonSerializerOptions JsonCamel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

  public static void MapCartHttpRoutes(this WebApplication app)
  {
    var group = app.MapGroup("/api/v1/cart")
        .WithTags("cart");

    if (app.Configuration.IsDcmsAuthEnabled())
      group.RequireAuthorization(DcmsPolicies.OrderAccess);

    group.MapGet("/", GetCart);
    group.MapPost("/lines", AddLine);
    group.MapPatch("/lines/{lineId}", UpdateLine);
    group.MapDelete("/lines/{lineId}", RemoveLine);
    group.MapDelete("/", ClearCart);

    var checkout = app.MapGroup("/api/v1/checkout")
        .WithTags("checkout");

    if (app.Configuration.IsDcmsAuthEnabled())
      checkout.RequireAuthorization(DcmsPolicies.OrderAccess);

    checkout.MapPost("/", Checkout);
  }

  private static async Task<IResult> GetCart(
      HttpContext http,
      [FromServices] ICartStore carts,
      [FromServices] IConfiguration configuration,
      CancellationToken cancellationToken)
  {
    if (!TryResolveScope(http, configuration, out var tenantId, out var storeId, out var ownerId, out var err))
      return err!;

    var cart = await carts.GetAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);
    return Results.Json(ToCartJson(cart ?? new CartSnapshot(tenantId, storeId, ownerId, [], DateTimeOffset.UtcNow)));
  }

  private static async Task<IResult> AddLine(
      HttpContext http,
      [FromBody] CartLineApiRequest? body,
      [FromServices] ICartStore carts,
      [FromServices] IConfiguration configuration,
      CancellationToken cancellationToken)
  {
    if (body is null)
      return ValidationError("Request body is required.");

    if (!TryResolveScope(http, configuration, out var tenantId, out var storeId, out var ownerId, out var err))
      return err!;

    if (!TryValidateLine(body, out var lineReq, out var validation))
      return validation!;

    var cart = await carts.UpsertLineAsync(tenantId, storeId, ownerId, lineReq, cancellationToken).ConfigureAwait(false);
    return Results.Json(ToCartJson(cart));
  }

  private static async Task<IResult> UpdateLine(
      string lineId,
      HttpContext http,
      [FromBody] CartLineQuantityRequest? body,
      [FromServices] ICartStore carts,
      [FromServices] IConfiguration configuration,
      CancellationToken cancellationToken)
  {
    if (body is null)
      return ValidationError("Request body is required.");

    if (!TryResolveScope(http, configuration, out var tenantId, out var storeId, out var ownerId, out var err))
      return err!;

    var cart = await carts
        .UpdateLineQuantityAsync(tenantId, storeId, ownerId, lineId, body.Quantity, cancellationToken)
        .ConfigureAwait(false);
    if (cart is null)
      return Results.Json(new { error = new { code = "NOT_FOUND", message = "Cart line not found." } }, statusCode: StatusCodes.Status404NotFound);

    return Results.Json(ToCartJson(cart));
  }

  private static async Task<IResult> RemoveLine(
      string lineId,
      HttpContext http,
      [FromServices] ICartStore carts,
      [FromServices] IConfiguration configuration,
      CancellationToken cancellationToken)
  {
    if (!TryResolveScope(http, configuration, out var tenantId, out var storeId, out var ownerId, out var err))
      return err!;

    var cart = await carts.RemoveLineAsync(tenantId, storeId, ownerId, lineId, cancellationToken).ConfigureAwait(false);
    if (cart is null)
      return Results.Json(new { error = new { code = "NOT_FOUND", message = "Cart line not found." } }, statusCode: StatusCodes.Status404NotFound);

    return Results.Json(ToCartJson(cart));
  }

  private static async Task<IResult> ClearCart(
      HttpContext http,
      [FromServices] ICartStore carts,
      [FromServices] IConfiguration configuration,
      CancellationToken cancellationToken)
  {
    if (!TryResolveScope(http, configuration, out var tenantId, out var storeId, out var ownerId, out var err))
      return err!;

    await carts.ClearAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);
    return Results.Json(ToCartJson(new CartSnapshot(tenantId, storeId, ownerId, [], DateTimeOffset.UtcNow)));
  }

  private static async Task<IResult> Checkout(
      HttpContext http,
      [FromBody] CheckoutApiRequest? body,
      [FromServices] ICartStore carts,
      [FromServices] IOrderService orders,
      [FromServices] QuantityLimitValidationService quantityLimits,
      [FromServices] IPromotionsClient promotions,
      [FromServices] IEntitlementGuard entitlementGuard,
      [FromServices] IConfiguration configuration,
      [FromServices] ILoggerFactory loggerFactory,
      CancellationToken cancellationToken)
  {
    if (!TryResolveScope(http, configuration, out var tenantId, out var storeId, out var ownerId, out var err))
      return err!;

    try
    {
      await entitlementGuard.EnsureFeatureAsync(tenantId, "orders.write", cancellationToken).ConfigureAwait(false);
    }
    catch (TenantEntitlementException ex)
    {
      return Results.Json(
          new { error = new { code = ex.Code, message = ex.Message } },
          statusCode: StatusCodes.Status403Forbidden);
    }

    var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
      return Results.Json(
          new { error = new { code = "MISSING_IDEMPOTENCY_KEY", message = "Idempotency-Key header is required." } },
          statusCode: StatusCodes.Status400BadRequest);
    }

    var cart = await carts.GetAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);
    if (cart is null || cart.Lines.Count == 0)
    {
      return Results.Json(
          new { error = new { code = "EMPTY_CART", message = "Cart is empty." } },
          statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    if (body?.ShippingAddress is null)
      return ValidationError("shippingAddress is required.");

    var ship = body.ShippingAddress;
    if (string.IsNullOrWhiteSpace(ship.Line1) || string.IsNullOrWhiteSpace(ship.City) ||
        string.IsNullOrWhiteSpace(ship.Region) || string.IsNullOrWhiteSpace(ship.PostalCode) ||
        string.IsNullOrWhiteSpace(ship.CountryCode))
      return ValidationError("shippingAddress.line1, city, region, postalCode, and countryCode are required.");

    var customerId = body.CustomerId?.Trim();
    if (string.IsNullOrWhiteSpace(customerId))
      customerId = ownerId.StartsWith("session:", StringComparison.Ordinal) ? null : ownerId;

    if (string.IsNullOrWhiteSpace(customerId))
      return ValidationError("customerId is required (or use an authenticated customer session).");

    if (OrderAuthorization.ValidateCreateOrderCustomer(http, configuration, customerId) is { } custErr)
      return custErr;

    var orderId = Guid.NewGuid().ToString();
    var lines = cart.Lines.Select(l => new CreateOrderLine(
        l.LineId,
        l.ProductId,
        l.VariantId,
        l.WarehouseId,
        l.Quantity,
        new Core.Domain.Money(l.UnitPriceAmount, l.Currency),
        l.ProductNameSnapshot,
        l.VariantSnapshotJson)).ToList();

    var command = new CreateOrderCommand(
        orderId,
        tenantId,
        storeId,
        customerId,
        idempotencyKey,
        lines,
        new Core.Domain.ShippingAddress(
            ship.Line1.Trim(),
            string.IsNullOrWhiteSpace(ship.Line2) ? null : ship.Line2.Trim(),
            ship.City.Trim(),
            ship.Region.Trim(),
            ship.PostalCode.Trim(),
            ship.CountryCode.Trim()),
        DateTimeOffset.UtcNow,
        body.CustomerName,
        body.CustomerEmail,
        body.CustomerPhone,
        PaymentMethod: string.IsNullOrWhiteSpace(body.PaymentMethod) ? "card" : body.PaymentMethod.Trim());

    var promoLogger = loggerFactory.CreateLogger("CartHttpRoutes.Promotions");
    var (enrichedCommand, promoErr) = await OrderHttpRoutes.EvaluatePromotionsAsync(
        promotions, configuration, promoLogger, command, body.PromoCode, cancellationToken).ConfigureAwait(false);
    if (promoErr is not null)
      return promoErr;

    command = enrichedCommand;

    try
    {
      await quantityLimits.EnsureCartValidAsync(
          tenantId,
          storeId,
          command.Lines.Select(l => new QuantityLimitCartLine(l.ProductId, l.Quantity)).ToList(),
          new QuantityLimitValidationContext(command.CustomerId, body.MembershipType, body.MembershipTier),
          cancellationToken).ConfigureAwait(false);
    }
    catch (QuantityLimitExceededException ex)
    {
      return Results.Json(
          new
          {
            error = new
            {
              code = "QUANTITY_LIMIT_EXCEEDED",
              message = ex.Message,
              productId = ex.ProductId,
              requested = ex.Requested,
              limit = ex.Limit,
            }
          },
          statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    try
    {
      var result = await orders.CreateOrderAsync(command, cancellationToken).ConfigureAwait(false);
      if (!result.IsIdempotentReplay)
        await carts.ClearAsync(tenantId, storeId, ownerId, cancellationToken).ConfigureAwait(false);

      var statusCode = result.IsIdempotentReplay ? StatusCodes.Status200OK : StatusCodes.Status201Created;
      return Results.Json(
          new
          {
            orderId = result.Order.Id,
            status = OrderHttpRoutes.ToApiStatus(result.Order.Status),
            paymentUrl = result.PaymentUrl,
          },
          statusCode: statusCode);
    }
    catch (OutOfStockException ex)
    {
      return Results.Json(
          new
          {
            error = new
            {
              code = "OUT_OF_STOCK",
              message = ex.Message,
              variantId = ex.VariantId,
              warehouseId = ex.WarehouseId,
            }
          },
          statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    catch (PaymentInitException ex)
    {
      return Results.Json(
          new { error = new { code = "PAYMENT_INIT_FAILED", message = ex.Message } },
          statusCode: StatusCodes.Status422UnprocessableEntity);
    }
  }

  private static object ToCartJson(CartSnapshot cart) => new
  {
    tenantId = cart.TenantId,
    storeId = cart.StoreId,
    ownerId = cart.OwnerId,
    updatedAt = cart.UpdatedAt,
    lines = cart.Lines.Select(l => new
    {
      lineId = l.LineId,
      productId = l.ProductId,
      variantId = l.VariantId,
      warehouseId = l.WarehouseId,
      quantity = l.Quantity,
      unitPrice = new { amount = l.UnitPriceAmount, currency = l.Currency },
      productNameSnapshot = l.ProductNameSnapshot,
      variantSnapshot = ParseJson(l.VariantSnapshotJson),
    }),
  };

  private static JsonElement? ParseJson(string json)
  {
    try
    {
      return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "{}" : json);
    }
    catch
    {
      return null;
    }
  }

  private static bool TryResolveScope(
      HttpContext http,
      IConfiguration configuration,
      out string tenantId,
      out string storeId,
      out string ownerId,
      out IResult? error)
  {
    tenantId = http.Request.Headers["X-Tenant-Id"].FirstOrDefault()?.Trim() ?? "";
    storeId = http.Request.Headers["X-Store-Id"].FirstOrDefault()?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(storeId))
    {
      error = Results.Json(
          new { error = new { code = "MISSING_TENANT_OR_STORE", message = "X-Tenant-Id and X-Store-Id headers are required." } },
          statusCode: StatusCodes.Status400BadRequest);
      ownerId = "";
      return false;
    }

    if (configuration.IsDcmsAuthEnabled())
    {
      var sub = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value?.Trim();
      if (!string.IsNullOrWhiteSpace(sub))
      {
        ownerId = sub;
        error = null;
        return true;
      }
    }

    var session = http.Request.Headers["X-Cart-Session"].FirstOrDefault()?.Trim();
    if (string.IsNullOrWhiteSpace(session))
    {
      error = Results.Json(
          new { error = new { code = "MISSING_CART_OWNER", message = "Authenticate as customer or send X-Cart-Session." } },
          statusCode: StatusCodes.Status400BadRequest);
      ownerId = "";
      return false;
    }

    ownerId = $"session:{session}";
    error = null;
    return true;
  }

  private static bool TryValidateLine(CartLineApiRequest body, out UpsertCartLineRequest line, out IResult? error)
  {
    line = null!;
    error = null;
    if (string.IsNullOrWhiteSpace(body.ProductId) || string.IsNullOrWhiteSpace(body.VariantId) ||
        string.IsNullOrWhiteSpace(body.WarehouseId) || body.Quantity <= 0 ||
        body.UnitPrice is null || body.UnitPrice.Amount <= 0 || string.IsNullOrWhiteSpace(body.UnitPrice.Currency))
    {
      error = ValidationError("productId, variantId, warehouseId, quantity, and unitPrice are required.");
      return false;
    }

    line = new UpsertCartLineRequest(
        body.LineId ?? "",
        body.ProductId,
        body.VariantId,
        body.WarehouseId,
        body.Quantity,
        body.UnitPrice.Amount,
        body.UnitPrice.Currency,
        body.ProductNameSnapshot ?? "",
        body.VariantSnapshot.GetRawText());
    return true;
  }

  private static IResult ValidationError(string message) =>
      Results.Json(new { error = new { code = "validation", message } }, statusCode: StatusCodes.Status400BadRequest);
}

public sealed class CartLineApiRequest
{
  public string? LineId { get; set; }
  public string? ProductId { get; set; }
  public string? VariantId { get; set; }
  public string? WarehouseId { get; set; }
  public int Quantity { get; set; }
  public MoneyApiRequest? UnitPrice { get; set; }
  public string? ProductNameSnapshot { get; set; }
  public JsonElement VariantSnapshot { get; set; }
}

public sealed class CartLineQuantityRequest
{
  public int Quantity { get; set; }
}

public sealed class CheckoutApiRequest
{
  public string? CustomerId { get; set; }
  public string? CustomerName { get; set; }
  public string? CustomerEmail { get; set; }
  public string? CustomerPhone { get; set; }
  public string? PromoCode { get; set; }
  public string? MembershipType { get; set; }
  public string? MembershipTier { get; set; }
  public string? PaymentMethod { get; set; }
  public CreateOrderShippingApiRequest? ShippingAddress { get; set; }
}
