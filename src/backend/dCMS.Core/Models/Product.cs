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
    public string NameJson { get; private set; } = null!;
    public string DescriptionJson { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public ProductStatus Status { get; private set; }
    public int SalesCount30d { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Product Create(
        string tenantId,
        string storeId,
        int categoryId,
        string nameJson,
        string descriptionJson,
        string slug,
        DateTimeOffset now)
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
            NameJson = nameJson,
            DescriptionJson = description,
            Slug = slug.Trim().ToLowerInvariant(),
            Status = ProductStatus.Draft,
            SalesCount30d = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        product._domainEvents.Add(new ProductCreated(id, tenantId, storeId, now));
        return product;
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
        DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            StoreId = storeId,
            CategoryId = categoryId,
            NameJson = nameJson,
            DescriptionJson = descriptionJson,
            Slug = slug,
            Status = status,
            SalesCount30d = salesCount30d,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
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

    /// <summary>SPU field updates (spec: UpdateProduct → ProductUpdated). Blocked when archived.</summary>
    public void UpdateDetails(int categoryId, string nameJson, string descriptionJson, string slug, DateTimeOffset now)
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

        if (CategoryId == categoryId && NameJson == nameJson && DescriptionJson == desc && Slug == normalized)
            return;

        CategoryId = categoryId;
        NameJson = nameJson;
        DescriptionJson = desc;
        Slug = normalized;
        Touch(now);
        _domainEvents.Add(new ProductUpdated(Id, TenantId, StoreId, now));
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
