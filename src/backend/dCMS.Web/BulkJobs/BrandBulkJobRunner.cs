using System.Globalization;
using System.Text.Json;
using dCMS.Core.Models;
using dCMS.Core.Persistence;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace dCMS.Web.BulkJobs;

/// <summary>
/// Brand bulk import (DAI-740). Round-trips the BrandsPage xlsx export:
///   - 5 first-class columns (Code/Name/Active/BrandImage/ContactPerson) map onto the Brand entity.
///   - Everything else (Description, MetaTitle, multi-language *_ZH/_VN/_JA suffixes, Y/N flags,
///     per-outlet counters, tenant-specific fields, …) folds into AdditionalInfo JSONB so a
///     subsequent export reproduces the input.
/// Tenant-scoped: uses the same TenantId discovered by the controller; brand `Code` is the PK
/// within the tenant via SaveBrandAsync upsert.
/// </summary>
public sealed class BrandBulkJobRunner
{
    private const int ProgressLogEveryN = 25;

    private readonly IBrandPersistence _brands;
    private readonly IBulkJobRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BrandBulkJobRunner> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] DateFormats =
    [
        "dd/MM/yyyy h:mm:ss tt", "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy HH:mm:ss",
        "MM/dd/yyyy h:mm:ss tt", "MM/dd/yyyy hh:mm:ss tt", "MM/dd/yyyy HH:mm:ss",
        "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
    ];

    // Headers that map to first-class Brand columns and should NOT bleed into AdditionalInfo.
    private static readonly HashSet<string> FirstClassHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "name", "active", "brandimage", "contactperson",
    };

    // Tenant column is informational in the export; always ignored on import.
    private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenant",
    };

    private static readonly string[] LangSuffixes = ["_zh", "_vn", "_ja", "_en"];

    public BrandBulkJobRunner(
        IBrandPersistence brands,
        IBulkJobRepository repo,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<BrandBulkJobRunner> log)
    {
        _brands = brands;
        _repo = repo;
        _configuration = configuration;
        _env = env;
        _log = log;
    }

    [AutomaticRetry(Attempts = 0)]
    [Queue("bulk")]
    [JobDisplayName("dCMS: brand import")]
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

            var parsed = await BulkImportRowReader.ReadAsync(inputPath, default).ConfigureAwait(false);
            if (parsed.Rows.Count == 0) throw new InvalidOperationException("No data rows.");

            // Schema detection: code + name present, no slug / categoryid (those are catalog).
            var hasCode = parsed.Headers.Contains("code", StringComparer.OrdinalIgnoreCase);
            var hasName = parsed.Headers.Contains("name", StringComparer.OrdinalIgnoreCase);
            var hasSlug = parsed.Headers.Contains("slug", StringComparer.OrdinalIgnoreCase);
            var hasCategoryId = parsed.Headers.Contains("categoryid", StringComparer.OrdinalIgnoreCase);
            if (!hasCode || !hasName || hasSlug || hasCategoryId)
                throw new InvalidOperationException(
                    "Unrecognized columns for brand import. Expected headers including 'Code' and 'Name' " +
                    "with no 'slug'/'categoryId' (which would indicate a catalog file).");

            int total = parsed.Rows.Count;
            await _repo.UpdateProgressAsync(tenantId, jobId, 0, total, 0, default).ConfigureAwait(false);
            BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.BrandImport).Inc();

            int processed = await ImportBrandsAsync(tenantId, jobId, parsed.Rows).ConfigureAwait(false);

            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false))
            {
                await _repo.MarkFinishedAsync(tenantId, jobId, "cancelled", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
                BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.BrandImport, "cancelled").Inc();
                return;
            }

            await _repo.UpdateProgressAsync(tenantId, jobId, processed, total, 100, default).ConfigureAwait(false);
            await _repo.MarkFinishedAsync(tenantId, jobId, "succeeded", DateTimeOffset.UtcNow, null, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.BrandImport, "succeeded").Inc();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Brand import failed for {Tenant} job {JobId}", tenantId, jobId);
            await _repo.MarkFinishedAsync(tenantId, jobId, "failed", DateTimeOffset.UtcNow, ex.Message, default).ConfigureAwait(false);
            BulkJobMetrics.Completed.WithLabels(tenantId, BulkJobKinds.BrandImport, "failed").Inc();
            throw;
        }
    }

    private async Task<int> ImportBrandsAsync(string tenantId, Guid jobId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var processed = 0;
        var total = rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            if (await IsCancelledAsync(tenantId, jobId, default).ConfigureAwait(false)) return processed;

            var row = rows[i];
            var rowNum = i + 2;

            var code = (row.GetValueOrDefault("code") ?? "").Trim();
            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException($"Line {rowNum}: Code is required.");
            if (!Brand.IsValidCode(code))
                throw new InvalidOperationException($"Line {rowNum}: Code '{code}' is invalid (1–64 chars: letters, digits, dashes).");

            var name = (row.GetValueOrDefault("name") ?? "").Trim();
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Line {rowNum}: Name is required.");

            var active = ParseBool(row.GetValueOrDefault("active"), defaultValue: true);
            var imageUrl = (row.GetValueOrDefault("brandimage") ?? "").Trim();
            var imageAlt = (row.GetValueOrDefault("contactperson") ?? "").Trim();
            var additionalInfo = BuildAdditionalInfo(row);

            var t = DateTimeOffset.UtcNow;
            var existing = await _brands.GetBrandAsync(tenantId, code, default).ConfigureAwait(false);
            if (existing is not null)
            {
                existing.UpdateDetails(name, active, imageUrl, imageAlt, additionalInfo, t);
                await _brands.SaveBrandAsync(existing, default).ConfigureAwait(false);
            }
            else
            {
                var brand = Brand.Create(tenantId, code, name, imageUrl, imageAlt, active, t, additionalInfo);
                await _brands.SaveBrandAsync(brand, default).ConfigureAwait(false);
            }

            processed++;
            await ReportProgressAsync(tenantId, jobId, processed, total).ConfigureAwait(false);
        }
        return processed;
    }

    /// <summary>
    /// Folds non-first-class columns into a JSONB-ready string. Multi-language suffixes
    /// (_zh/_vn/_ja/_en) collapse into nested locale maps so consumers can read
    /// <c>{ crmDescription: { en, zh } }</c>. Empty values are dropped to keep the JSON small.
    /// </summary>
    private static string BuildAdditionalInfo(IReadOnlyDictionary<string, string> row)
    {
        var simple = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var multilang = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawHeader, rawValue) in row)
        {
            if (string.IsNullOrWhiteSpace(rawHeader)) continue;
            var header = rawHeader.Trim();
            if (FirstClassHeaders.Contains(header) || IgnoredHeaders.Contains(header)) continue;

            var value = rawValue?.Trim();
            if (string.IsNullOrEmpty(value)) continue;

            var (baseKey, locale) = SplitLanguageSuffix(header);
            if (locale is not null)
            {
                if (!multilang.TryGetValue(baseKey, out var map))
                {
                    map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    multilang[baseKey] = map;
                }
                map[locale] = value;
                continue;
            }

            simple[CamelCase(header)] = ParseScalar(value);
        }

        // Promote any base key whose only entry is the default (en) value already stored
        // in `simple` into the multilang group, then emit nested objects.
        foreach (var (baseKey, map) in multilang)
        {
            var camelBase = CamelCase(baseKey);
            if (simple.Remove(camelBase, out var defaultValue) && defaultValue is string defaultStr)
                map.TryAdd("en", defaultStr);
            simple[camelBase] = map.ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => kv.Value);
        }

        return JsonSerializer.Serialize(simple, JsonOpts);
    }

    private static (string BaseKey, string? Locale) SplitLanguageSuffix(string header)
    {
        foreach (var suffix in LangSuffixes)
        {
            if (header.Length > suffix.Length &&
                header.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return (header[..^suffix.Length], suffix.TrimStart('_').ToLowerInvariant());
            }
        }
        return (header, null);
    }

    /// <summary>Best-effort scalar coercion: True/False, numbers, otherwise raw string.</summary>
    private static object ParseScalar(string value)
    {
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("no", StringComparison.OrdinalIgnoreCase))
            return false;
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return l;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d;
        return value;
    }

    private static string CamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Split on '_' and ' ' so headers like "Available_Online" → "availableOnline".
        var parts = s.Split(new[] { '_', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return s.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToLowerInvariant(parts[0][0]));
        if (parts[0].Length > 1) sb.Append(parts[0][1..]);
        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            sb.Append(char.ToUpperInvariant(parts[i][0]));
            if (parts[i].Length > 1) sb.Append(parts[i][1..]);
        }
        return sb.ToString();
    }

    private async Task ReportProgressAsync(string tenantId, Guid jobId, int processed, int total)
    {
        if (processed % ProgressLogEveryN != 0 && processed != total) return;
        var percent = total == 0 ? 0 : (int)(100.0 * processed / total);
        await _repo.UpdateProgressAsync(tenantId, jobId, processed, total, percent, default).ConfigureAwait(false);
        BulkJobMetrics.ProgressUpdates.WithLabels(tenantId, BulkJobKinds.BrandImport).Inc();
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
        return s.Equals("1", StringComparison.Ordinal)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("y", StringComparison.OrdinalIgnoreCase);
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
}
