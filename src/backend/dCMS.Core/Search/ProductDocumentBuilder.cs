using System.Text.Json;
using dCMS.Core.Models;

namespace dCMS.Core.Search;

/// <summary>Pure builder: <see cref="ProductIndexPayload"/> → <see cref="ProductDocument"/> (testable, no I/O).</summary>
public static class ProductDocumentBuilder
{
    public static ProductDocument Build(ProductIndexPayload input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var product = input.Product;
        ArgumentNullException.ThrowIfNull(product);

        var name = ParseObjectStringValues(product.NameJson);
        var variants = new List<VariantDocument>();
        long minBase = long.MaxValue;
        foreach (var v in input.Variants)
        {
            var stock = input.StockByVariantId.TryGetValue(v.Id, out var s)
                ? s
                : new VariantStockSummary(0, false);
            var basePrice = new MoneyAmount(Math.Max(0, v.BasePriceAmount), input.StoreCurrency);
            if (basePrice.Amount < minBase)
                minBase = basePrice.Amount;

            variants.Add(new VariantDocument
            {
                VariantId = v.Id,
                Sku = v.Sku,
                Status = v.Status,
                AvailableQty = stock.AvailableQty,
                InStock = stock.InStock,
                BasePrice = basePrice,
                Attributes = new Dictionary<string, object>()
            });
        }

        if (minBase == long.MaxValue)
            minBase = 0;

        var hasInStock = variants.Exists(static x => x.InStock);

        return new ProductDocument
        {
            Id = product.Id,
            TenantId = product.TenantId,
            StoreId = product.StoreId,
            BrandId = input.BrandId,
            CategoryId = product.CategoryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CategoryPath = input.CategoryPath,
            CategoryAncestors = input.CategoryAncestors,
            Name = name,
            Slug = product.Slug,
            Status = product.Status.ToPersistedValue(),
            StoreCurrency = input.StoreCurrency,
            SalesCount30d = product.SalesCount30d,
            Attributes = new Dictionary<string, string>(input.AttributesFlattened, StringComparer.Ordinal),
            Variants = variants,
            HasInStockVariant = hasInStock,
            MinBasePrice = new MoneyAmount(minBase, input.StoreCurrency),
            SnapshotVersion = input.SnapshotVersion,
            UpdatedAt = product.UpdatedAt,
            ActiveSalePrice = null
        };
    }

    public static Dictionary<string, string> ParseObjectStringValues(string json)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return d;
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                d[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => p.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => p.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
            // Malformed JSON in DB — treat as empty labels rather than failing the indexer.
        }

        return d;
    }
}
