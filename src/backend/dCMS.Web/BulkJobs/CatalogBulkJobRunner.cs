using System.Globalization;
using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using dCMS.Core.ValueObjects;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Web.BulkJobs;

public sealed class CatalogBulkJobRunner
{
    private const int ProgressLogEveryN = 25;

    private readonly ICatalogPersistence _catalog;
    private readonly IBulkJobRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CatalogBulkJobRunner> _log;

    private static readonly JsonSerializerOptions NameJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Date formats accepted in Categories XLSX (PublishedFrom/To columns).
    // Export emits "dd/MM/yyyy h:mm:ss tt"; we also accept ISO 8601.
    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy h:mm:ss tt", "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy HH:mm:ss",
        "MM/dd/yyyy h:mm:ss tt", "MM/dd/yyyy hh:mm:ss tt", "MM/dd/yyyy HH:mm:ss",
        "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
    ];

    public CatalogBulkJobRunner(
        ICatalogPersistence catalog,
        IBulkJobRepository repo,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<CatalogBulkJobRunner> log)
    {
        _catalog = catalog;
        _repo = repo;
        _configuration = configuration;
        _env = env;
        _log = log;
    }

    [AutomaticRetry(Attempts = 0)]
    [Queue("bulk")]
    [JobDisplayName("dCMS: catalog import")]
    public async Task RunAsync(string tenantId, Guid jobId)
    {
        var now = DateTimeOffset.UtcNow;
        await _repo.MarkStartedAsync(tenantId, jobId, now, default).ConfigureAwait(false);

        try
        {
            var job = await _repo.GetByIdAsync(tenantId, jobId, default).ConfigureAwait(false);
            if (job is null) throw new InvalidOperationException("Job not found.");
            if (string.IsNullOrWhiteSpace(job.InputBlobRef)) throw new InvalidOperationException("Missing input file.");

            var inputPath = BulkJobPathHelper.ResolveUnderContentRoot(_env, job.InputBlobRef);
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file missing.", inputPath);

            var storeId = job.StoreId?.Trim() ?? _configuration["Dcms:Estore:StoreId"]?.Trim() ?? throw new InvalidOperationException("StoreId required.");

            var parsed = await BulkImportRowReader.ReadAsync(inputPath, default).ConfigureAwait(false);
            if (parsed.Rows.Count == 0) throw new InvalidOperationException("No data rows.");

            // Detect schema by header set: categories ⇒ "code"+"name"; products ⇒ "slug"+"categoryid".
            var hasCode = parsed.Headers.Contains("code", StringComparer.OrdinalIgnoreCase);
            var hasName = parsed.Headers.Contains("name", StringComparer.OrdinalIgnoreCase);
            var hasSlug = parsed.Headers.Contains("slug", StringComparer.OrdinalIgnoreCase);
            var hasCategoryId = parsed.Headers.Contains("categoryid", StringComparer.OrdinalIgnoreCase);

            int processed;
            int total = parsed.Rows.Count;
            await _repo.UpdateProgressAsync(tenantId, jobId, 0, total, 0, default).ConfigureAwait(false);
            BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.CatalogImport).Inc();

            if (hasSlug && hasCategoryId)
                processed = await ImportProductsAsync(tenantId, jobId, storeId, parsed.Rows).ConfigureAwait(false);
            else if (hasCode && hasName)
                processed = await ImportCategoriesAsync(tenantId, jobId, parsed.Rows).ConfigureAwait(false);
            else
                throw new InvalidOperationException(
                    "Unrecognized columns. Expected products (slug, categoryId, nameVi, [descriptionVi]) " +
                    "or categories (Code, Name, MetaTitle, MetaKeywords, MetaDescription, Active, PublishedFrom, PublishedTo).");

            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false))
            {
                await _repo.MarkFinishedAsync(tenantId, jobId, "cancelled", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
                BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.CatalogImport, "cancelled").Inc();
                return;
            }

            await _repo.UpdateProgressAsync(tenantId, jobId, processed, total, 100, default).ConfigureAwait(false);
            await _repo.MarkFinishedAsync(tenantId, jobId, "succeeded", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.CatalogImport, "succeeded").Inc();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Catalog import failed for {Tenant} job {JobId}", tenantId, jobId);
            await _repo.MarkFinishedAsync(tenantId, jobId, "failed", DateTimeOffset.UtcNow, ex.Message, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.CatalogImport, "failed").Inc();
            throw;
        }
    }

    // ── Products (legacy CSV) ────────────────────────────────────────────────
    private async Task<int> ImportProductsAsync(string tenantId, Guid jobId, string storeId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var processed = 0;
        var total = rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false)) return processed;

            var row = rows[i];
            var rowNum = i + 2; // 1-based + header
            var slug = (row.GetValueOrDefault("slug") ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(slug))
                throw new InvalidOperationException($"Line {rowNum}: slug is required.");

            var categoryIdRaw = row.GetValueOrDefault("categoryid") ?? "";
            if (!int.TryParse(categoryIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId) || categoryId <= 0)
                throw new InvalidOperationException($"Line {rowNum}: invalid categoryId.");

            var nameVi = row.GetValueOrDefault("namevi") ?? "";
            var desc = row.GetValueOrDefault("descriptionvi") ?? "";

            var cat = await _catalog.GetCategoryByIdAsync(categoryId, tenantId, default).ConfigureAwait(false);
            if (cat is null) throw new InvalidOperationException($"Line {rowNum}: category {categoryId} not found for tenant.");

            var nameJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["vi"] = nameVi }, NameJson);
            var descJson = string.IsNullOrWhiteSpace(desc)
                ? "{}"
                : JsonSerializer.Serialize(new Dictionary<string, string> { ["vi"] = desc }, NameJson);

            MultilangJson.ValidateNameRequiredVi(nameJson);
            MultilangJson.ValidateDescriptionOptional(descJson);

            var t = DateTimeOffset.UtcNow;
            var existing = await _catalog.GetBySlugAsync(storeId, tenantId, slug, default).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.Status == ProductStatus.Archived)
                    throw new InvalidOperationException($"Line {rowNum}: product {slug} is archived.");
                existing.UpdateDetails(categoryId, nameJson, descJson, slug, t);
                await _catalog.SaveProductWithOutboxAsync(existing, default).ConfigureAwait(false);
            }
            else
            {
                var p = Product.Create(tenantId, storeId, categoryId, nameJson, descJson, slug, t);
                await _catalog.SaveProductWithOutboxAsync(p, default).ConfigureAwait(false);
            }

            processed++;
            await ReportProgressAsync(tenantId, jobId, processed, total).ConfigureAwait(false);
        }
        return processed;
    }

    // ── Categories (XLSX export round-trip) ──────────────────────────────────
    // Code is hierarchical with '-' separator (e.g. 11-11-FASHION-MEN ⇒ parent 11-11-FASHION).
    // Two-pass: sort ascending by depth so parents are upserted before children.
    private async Task<int> ImportCategoriesAsync(string tenantId, Guid jobId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        // Pre-validate + index by code. Keep raw code (original casing → Internal Identifier)
        // and lowercased slug (URL-safe + parent lookup).
        var indexed = new List<(int Order, string RawCode, string Slug, IReadOnlyDictionary<string, string> Row)>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var rawCode = (r.GetValueOrDefault("code") ?? "").Trim();
            if (string.IsNullOrEmpty(rawCode))
                throw new InvalidOperationException($"Line {i + 2}: Code is required.");
            indexed.Add((i, rawCode, rawCode.ToLowerInvariant(), r));
        }

        // Stable sort by depth (number of '-' segments) so parents land first.
        var ordered = indexed
            .OrderBy(x => x.Slug.Count(c => c == '-'))
            .ThenBy(x => x.Order)
            .ToList();

        var processed = 0;
        var total = ordered.Count;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false)) return processed;

            var (origOrder, rawCode, slug, row) = ordered[i];
            var rowNum = origOrder + 2;

            var name = (row.GetValueOrDefault("name") ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Line {rowNum}: Name is required.");

            // Resolve parent by stripping trailing '-segment' from Code. Codes can contain
            // dashes inside a single segment (e.g. "05-29OCT-20-FASHION" is top-level), so a
            // missing prefix is NOT an error — it just means the row is a root category.
            int? parentId = null;
            var lastDash = slug.LastIndexOf('-');
            if (lastDash > 0)
            {
                var parentSlug = slug[..lastDash];
                var parent = await _catalog.GetCategoryBySlugAsync(tenantId, parentSlug, default).ConfigureAwait(false);
                if (parent is not null) parentId = parent.Id;
            }

            var active = ParseBool(row.GetValueOrDefault("active"), defaultValue: true);
            var publishFrom = ParseDate(row.GetValueOrDefault("publishedfrom"));
            var publishUntil = ParseDate(row.GetValueOrDefault("publishedto"));

            var metaTitle = row.GetValueOrDefault("metatitle") ?? "";
            var metaKeywords = row.GetValueOrDefault("metakeywords") ?? "";
            var metaDesc = row.GetValueOrDefault("metadescription") ?? "";

            var metaTitleJson = ToEnJson(metaTitle);
            var metaKeywordsJson = ToEnJson(metaKeywords);
            var metaDescJson = ToEnJson(metaDesc);

            var existing = await _catalog.GetCategoryBySlugAsync(tenantId, slug, default).ConfigureAwait(false);
            if (existing is not null)
            {
                var updated = existing with
                {
                    Name = name,
                    Code = rawCode,
                    Active = active,
                    PublishFrom = publishFrom,
                    PublishUntil = publishUntil,
                    MetaTitleJson = metaTitleJson,
                    MetaKeywordsJson = metaKeywordsJson,
                    MetaDescJson = metaDescJson,
                };
                await _catalog.UpdateCategoryAsync(updated, default).ConfigureAwait(false);

                // Reclassify if hierarchy changed.
                if (existing.ParentId != parentId)
                {
                    try
                    {
                        await _catalog.ReclassifyCategoryAsync(existing.Id, tenantId, parentId, default).ConfigureAwait(false);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InvalidOperationException($"Line {rowNum}: cannot move '{slug}': {ex.Message}");
                    }
                }
            }
            else
            {
                var fresh = new CatalogCategoryRow(
                    Id: 0, TenantId: tenantId, ParentId: parentId, Path: "/", Depth: 0,
                    Name: name, Slug: slug, SortOrder: 0,
                    Code: rawCode,
                    Active: active, PublishFrom: publishFrom, PublishUntil: publishUntil,
                    ImageMenuUrl: "", ImagePageUrl: "", ImageThumbUrl: "",
                    ShowInNav: true, ShowInBrands: false, CustomNavUrl: "",
                    NavSortPriority: 10, BreakNavColumn: false,
                    DefaultSort: "bestseller", NoRecommendations: false,
                    MetaTitleJson: metaTitleJson, MetaKeywordsJson: metaKeywordsJson, MetaDescJson: metaDescJson,
                    RestrictAccess: false, AccessApp: "", AccessMemberType: "", AccessMemberTier: "");
                try
                {
                    await _catalog.CreateCategoryAsync(fresh, default).ConfigureAwait(false);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException($"Line {rowNum}: {ex.Message}");
                }
            }

            processed++;
            await ReportProgressAsync(tenantId, jobId, processed, total).ConfigureAwait(false);
        }
        return processed;
    }

    private async Task ReportProgressAsync(string tenantId, Guid jobId, int processed, int total)
    {
        if (processed % ProgressLogEveryN != 0 && processed != total) return;
        var percent = total == 0 ? 0 : (int)(100.0 * processed / total);
        await _repo.UpdateProgressAsync(tenantId, jobId, processed, total, percent, default).ConfigureAwait(false);
        BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.CatalogImport).Inc();
    }

    private async Task<bool> IsCancelledAsync(string tenantId, Guid jobId, CancellationToken ct)
    {
        var j = await _repo.GetByIdAsync(tenantId, jobId, ct).ConfigureAwait(false);
        return j?.CancelRequestedAt is not null;
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        var s = raw.Trim();
        if (bool.TryParse(s, out var b)) return b;
        return s.Equals("1", StringComparison.Ordinal) || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (DateTimeOffset.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
            return exact;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var loose))
            return loose;
        return null;
    }

    // Category SEO meta JSON is rendered by the backoffice MultiLangField under the tenant's
    // default locale slot (en — see useUmbracoLanguages defaults). The CategoryExport file is
    // English copy, so import stores under "en" to round-trip into the visible EN tab.
    private static string ToEnJson(string raw)
    {
        var v = raw?.Trim();
        if (string.IsNullOrEmpty(v) || v.Equals("None", StringComparison.OrdinalIgnoreCase)) return "{}";
        return JsonSerializer.Serialize(new Dictionary<string, string> { ["en"] = v }, NameJson);
    }
}
