using System.Text.RegularExpressions;

namespace dCMS.Core.Models;

/// <summary>
/// Brand aggregate — tenant-scoped master data.
/// Sits at Tenant → Brand → Stores hierarchy.
/// No approval workflow; no domain events in this iteration.
/// </summary>
public sealed class Brand
{
    private static readonly Regex CodePattern =
        new(@"^[A-Z]{2,5}-[0-9]{1,6}$", RegexOptions.Compiled);

    private Brand() { }

    public string TenantId      { get; private set; } = null!;
    public string Code          { get; private set; } = null!;   // PK within tenant
    public string Name          { get; private set; } = null!;
    public bool   Active        { get; private set; }
    public string ImageUrl      { get; private set; } = string.Empty;
    public string ImageAlt      { get; private set; } = string.Empty;
    /// <summary>
    /// Dynamic additional-info fields configured in BrandConfigPage.
    /// Stored as raw JSON object string (e.g. <c>{"field-id-1":"value","field-id-2":["a","b"]}</c>).
    /// </summary>
    public string AdditionalInfo { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────

    public static Brand Create(
        string tenantId,
        string code,
        string name,
        string imageUrl,
        string imageAlt,
        bool active,
        DateTimeOffset now,
        string additionalInfo = "{}")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!CodePattern.IsMatch(code))
            throw new ArgumentException(
                $"Brand code '{code}' is invalid. Expected format: 2–5 uppercase letters, dash, 1–6 digits (e.g. CAS-7721).",
                nameof(code));

        if (name.Length > 200)
            throw new ArgumentException("Brand name must be 200 characters or fewer.", nameof(name));

        return new Brand
        {
            TenantId       = tenantId,
            Code           = code.Trim().ToUpperInvariant(),
            Name           = name.Trim(),
            Active         = active,
            ImageUrl       = (imageUrl ?? string.Empty).Trim(),
            ImageAlt       = (imageAlt ?? string.Empty).Trim(),
            AdditionalInfo = string.IsNullOrWhiteSpace(additionalInfo) ? "{}" : additionalInfo,
            CreatedAt      = now,
            UpdatedAt      = now,
        };
    }

    /// <summary>Rehydrate from persistence layer.</summary>
    public static Brand Restore(
        string tenantId,
        string code,
        string name,
        bool active,
        string imageUrl,
        string imageAlt,
        string additionalInfo,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            TenantId       = tenantId,
            Code           = code,
            Name           = name,
            Active         = active,
            ImageUrl       = imageUrl       ?? string.Empty,
            ImageAlt       = imageAlt       ?? string.Empty,
            AdditionalInfo = string.IsNullOrWhiteSpace(additionalInfo) ? "{}" : additionalInfo,
            CreatedAt      = createdAt,
            UpdatedAt      = updatedAt,
        };

    // ── Mutations ─────────────────────────────────────────────────────────────

    public void UpdateDetails(
        string name,
        bool active,
        string imageUrl,
        string imageAlt,
        string additionalInfo,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200)
            throw new ArgumentException("Brand name must be 200 characters or fewer.", nameof(name));

        Name           = name.Trim();
        Active         = active;
        ImageUrl       = (imageUrl ?? string.Empty).Trim();
        ImageAlt       = (imageAlt ?? string.Empty).Trim();
        AdditionalInfo = string.IsNullOrWhiteSpace(additionalInfo) ? "{}" : additionalInfo;
        UpdatedAt      = now;
    }

    public static bool IsValidCode(string code) =>
        !string.IsNullOrWhiteSpace(code) && CodePattern.IsMatch(code);
}
