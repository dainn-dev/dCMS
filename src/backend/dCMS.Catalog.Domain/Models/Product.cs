using dCMS.Core.Events;
using dCMS.Core.Exceptions;
using dCMS.Core.ValueObjects;

namespace dCMS.Core.Models;

/// <summary>
/// SPU aggregate (no embedded variants). Persisted <see cref="Status"/> never uses <c>out_of_stock</c> — availability is derived from Inventory / VariantStock (spec).
/// Dynamic SPU attributes use <c>ProductAttributeValues</c> in DB (later milestone); US-1 covers lifecycle + catalog fields.
/// See docs/superpowers/specs/2026-04-06-product-catalog-design.md.
/// </summary>
public sealed class Product
{
    private Product() { }

    private readonly List<IDomainEvent> _domainEvents = new();

    public string Id { get; private set; } = null!;
    public string TenantId { get; private set; } = null!;
    public string StoreId { get; private set; } = null!;
    public int CategoryId { get; private set; }
    /// <summary>Optional brand code this product belongs to (organizational layer inside the tenant).</summary>
    public string? BrandId { get; private set; }
    public string NameJson { get; private set; } = null!;
    public string DescriptionJson { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public ProductStatus Status { get; private set; }
    public int SalesCount30d { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // ── Product Page / SEO metadata (multi-language JSON values) ──
    public string PageTitleJson { get; private set; } = "{}";
    public string MetaKeywordsJson { get; private set; } = "{}";
    public string MetaDescriptionJson { get; private set; } = "{}";

    // ── Storefront publish window (UTC). null = unbounded. ──
    public DateTimeOffset? PublishFrom { get; private set; }
    public DateTimeOffset? PublishUntil { get; private set; }

    // ── Visibility / recommendation behaviour flags ──
    public bool RecommendSimilar { get; private set; } = true;
    public string RecommendationsMode { get; private set; } = "auto";
    public bool RestockNotification { get; private set; }

    /// <summary>Custom field values keyed by field id (from StoreProductFieldConfig).</summary>
    public string CustomFieldsJson { get; private set; } = "{}";

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Product Create(
        string tenantId,
        string storeId,
        int categoryId,
        string nameJson,
        string descriptionJson,
        string slug,
        DateTimeOffset now,
        string? brandId = null)
    {
        MultilangJson.ValidateNameRequiredVi(nameJson);
        MultilangJson.ValidateDescriptionOptional(descriptionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (categoryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(categoryId));

        var description = string.IsNullOrWhiteSpace(descriptionJson) ? "{}" : descriptionJson;
        var id = "prod_" + Guid.NewGuid().ToString("N");
        var product = new Product
        {
            Id = id,
            TenantId = tenantId,
            StoreId = storeId,
            CategoryId = categoryId,
            BrandId = NormalizeBrandId(brandId),
            NameJson = nameJson,
            DescriptionJson = description,
            Slug = slug.Trim().ToLowerInvariant(),
            Status = ProductStatus.Draft,
            SalesCount30d = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CustomFieldsJson = "{}"
        };
        product._domainEvents.Add(new ProductCreated(id, tenantId, storeId, now));
        return product;
    }

    private static string? NormalizeBrandId(string? brandId)
    {
        var b = brandId?.Trim();
        return string.IsNullOrEmpty(b) ? null : b;
    }

    /// <summary>Rehydrate from persistence (Infrastructure).</summary>
    public static Product Restore(
        string id,
        string tenantId,
        string storeId,
        int categoryId,
        string nameJson,
        string descriptionJson,
        string slug,
        ProductStatus status,
        int salesCount30d,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? brandId = null,
        string? pageTitleJson = null,
        string? metaKeywordsJson = null,
        string? metaDescriptionJson = null,
        DateTimeOffset? publishFrom = null,
        DateTimeOffset? publishUntil = null,
        bool recommendSimilar = true,
        string? recommendationsMode = null,
        bool restockNotification = false,
        string? customFieldsJson = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            StoreId = storeId,
            CategoryId = categoryId,
            BrandId = NormalizeBrandId(brandId),
            NameJson = nameJson,
            DescriptionJson = descriptionJson,
            Slug = slug,
            Status = status,
            SalesCount30d = salesCount30d,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            PageTitleJson = NormalizeJson(pageTitleJson),
            MetaKeywordsJson = NormalizeJson(metaKeywordsJson),
            MetaDescriptionJson = NormalizeJson(metaDescriptionJson),
            PublishFrom = publishFrom,
            PublishUntil = publishUntil,
            RecommendSimilar = recommendSimilar,
            RecommendationsMode = NormalizeMode(recommendationsMode),
            RestockNotification = restockNotification,
            CustomFieldsJson = NormalizeJson(customFieldsJson)
        };

    /// <summary>draft → pending_approval</summary>
    public void SubmitForApproval(DateTimeOffset now)
    {
        if (Status != ProductStatus.Draft)
            throw new InvalidProductStateException("Only draft products can be submitted for approval.");

        Status = ProductStatus.PendingApproval;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>pending_approval → draft (request changes / reject).</summary>
    public void ReturnPendingToDraft(DateTimeOffset now)
    {
        if (Status != ProductStatus.PendingApproval)
            throw new InvalidProductStateException("Only products pending approval can be returned to draft.");

        Status = ProductStatus.Draft;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>draft | pending_approval | hidden → active. Throws if archived.</summary>
    public void Publish(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot publish an archived product.");

        if (Status == ProductStatus.Active)
            return;

        Status = ProductStatus.Active;
        Touch(now);
        _domainEvents.Add(new ProductPublished(Id, TenantId, StoreId, now));
    }

    public void Hide(DateTimeOffset now)
    {
        if (Status != ProductStatus.Active)
            throw new InvalidProductStateException("Only active products can be hidden.");

        Status = ProductStatus.Hidden;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    public void Unhide(DateTimeOffset now)
    {
        if (Status != ProductStatus.Hidden)
            throw new InvalidProductStateException("Only hidden products can be restored to active.");

        Status = ProductStatus.Active;
        Touch(now);
        _domainEvents.Add(new ProductPublished(Id, TenantId, StoreId, now));
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
            return;

        Status = ProductStatus.Archived;
        Touch(now);
        _domainEvents.Add(new ProductArchived(Id, TenantId, StoreId, now));
    }

    /// <summary>active | hidden → pending_archive (archive request awaiting approval).</summary>
    public void SubmitForArchive(DateTimeOffset now)
    {
        if (Status is not (ProductStatus.Active or ProductStatus.Hidden))
            throw new InvalidProductStateException("Only active or hidden products can be sent for archive.");

        Status = ProductStatus.PendingArchive;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>pending_archive → archived (approver accepts the archive request).</summary>
    public void ApproveArchive(DateTimeOffset now)
    {
        if (Status != ProductStatus.PendingArchive)
            throw new InvalidProductStateException("Only products pending archive can be archived this way.");

        Status = ProductStatus.Archived;
        Touch(now);
        _domainEvents.Add(new ProductArchived(Id, TenantId, StoreId, now));
    }

    /// <summary>pending_archive → active (archive request rejected / cancelled).</summary>
    public void CancelArchiveRequest(DateTimeOffset now)
    {
        if (Status != ProductStatus.PendingArchive)
            throw new InvalidProductStateException("Only products pending archive can have their request cancelled.");

        Status = ProductStatus.Active;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>SPU field updates (spec: UpdateProduct → ProductUpdated). Blocked when archived.</summary>
    public void UpdateDetails(int categoryId, string nameJson, string descriptionJson, string slug, DateTimeOffset now,
        string? brandId = null)
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot update an archived product.");

        MultilangJson.ValidateNameRequiredVi(nameJson);
        MultilangJson.ValidateDescriptionOptional(descriptionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (categoryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(categoryId));

        var normalized = slug.Trim().ToLowerInvariant();
        var desc = string.IsNullOrWhiteSpace(descriptionJson) ? "{}" : descriptionJson;
        var normalizedBrand = NormalizeBrandId(brandId);

        if (CategoryId == categoryId && NameJson == nameJson && DescriptionJson == desc && Slug == normalized &&
            string.Equals(BrandId, normalizedBrand, StringComparison.Ordinal))
            return;

        CategoryId = categoryId;
        NameJson = nameJson;
        DescriptionJson = desc;
        Slug = normalized;
        BrandId = normalizedBrand;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>
    /// Updates Product Page / SEO metadata, the storefront publish window and visibility/recommendation flags.
    /// No-ops (no event) when nothing changed. Blocked when archived.
    /// </summary>
    public void UpdatePageMetadata(ProductPageMetadata metadata, DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot update an archived product.");

        ArgumentNullException.ThrowIfNull(metadata);

        var pageTitle = NormalizeJson(metadata.PageTitleJson);
        var metaKeywords = NormalizeJson(metadata.MetaKeywordsJson);
        var metaDescription = NormalizeJson(metadata.MetaDescriptionJson);
        var mode = NormalizeMode(metadata.RecommendationsMode);

        if (metadata.PublishFrom is { } from && metadata.PublishUntil is { } until && from > until)
            throw new ArgumentException("Publish window start must be on or before the end date.");

        MultilangJson.ValidateDescriptionOptional(pageTitle);
        MultilangJson.ValidateDescriptionOptional(metaKeywords);
        MultilangJson.ValidateDescriptionOptional(metaDescription);

        var unchanged =
            PageTitleJson == pageTitle &&
            MetaKeywordsJson == metaKeywords &&
            MetaDescriptionJson == metaDescription &&
            Nullable.Equals(PublishFrom, metadata.PublishFrom) &&
            Nullable.Equals(PublishUntil, metadata.PublishUntil) &&
            RecommendSimilar == metadata.RecommendSimilar &&
            RecommendationsMode == mode &&
            RestockNotification == metadata.RestockNotification;
        if (unchanged)
            return;

        PageTitleJson = pageTitle;
        MetaKeywordsJson = metaKeywords;
        MetaDescriptionJson = metaDescription;
        PublishFrom = metadata.PublishFrom;
        PublishUntil = metadata.PublishUntil;
        RecommendSimilar = metadata.RecommendSimilar;
        RecommendationsMode = mode;
        RestockNotification = metadata.RestockNotification;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>Replaces custom field values (validated upstream). Blocked when archived.</summary>
    public void UpdateCustomFields(string customFieldsJson, DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot update an archived product.");

        var normalized = NormalizeJson(customFieldsJson);
        if (CustomFieldsJson == normalized)
            return;

        CustomFieldsJson = normalized;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    private static string NormalizeJson(string? json) =>
        string.IsNullOrWhiteSpace(json) ? "{}" : json.Trim();

    private static string NormalizeMode(string? mode)
    {
        var m = (mode ?? "auto").Trim().ToLowerInvariant();
        return m is "auto" or "manual" or "disabled" ? m : "auto";
    }

    /// <summary>After variant generation — raises <see cref="ProductUpdated"/> (spec: GenerateVariants → ProductUpdated).</summary>
    public void RecordVariantsGenerated(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot generate variants for an archived product.");

        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    /// <summary>After variant SKU/price/status rows change — reindex + outbox (US-14).</summary>
    public void RecordVariantRowsUpdated(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductStateException("Cannot update variants for an archived product.");

        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
