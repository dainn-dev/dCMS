using System.Text;
using ClosedXML.Excel;

namespace dCMS.Web.BulkJobs;

/// <summary>
/// Sniffs file magic bytes to read XLSX (PK\x03\x04) or CSV. Returns a header list plus
/// row dictionaries keyed by lower-cased header name, mirroring the dCMS.Catalog.Worker
/// XlsxStreamReader pattern (DAI-707) — but local to dCMS.Web so the bulk-jobs runner
/// doesn't take a dependency on the worker assembly.
/// </summary>
internal static class BulkImportRowReader
{
    public sealed record ParsedFile(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

    public static async Task<ParsedFile> ReadAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var head = new byte[4];
        var read = await fs.ReadAsync(head.AsMemory(0, 4), ct).ConfigureAwait(false);
        fs.Position = 0;

        bool isXlsx = read >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;
        return isXlsx ? ReadXlsx(fs) : await ReadCsvAsync(fs, ct).ConfigureAwait(false);
    }

    private static ParsedFile ReadXlsx(Stream s)
    {
        using var wb = new XLWorkbook(s);
        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("XLSX has no worksheet.");

        string[]? headers = null;
        var rows = new List<IReadOnlyDictionary<string, string>>();

        foreach (var r in ws.RowsUsed())
        {
            var lastCol = r.LastCellUsed()?.Address.ColumnNumber ?? 0;
            if (lastCol == 0) continue;
            var values = r.Cells(1, lastCol).Select(c => c.GetString().Trim()).ToArray();
            if (values.Length == 0 || values.All(string.IsNullOrWhiteSpace)) continue;

            if (headers is null)
            {
                headers = values.Select(v => v.ToLowerInvariant()).ToArray();
                continue;
            }

            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length && i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(headers[i]))
                    cells[headers[i]] = values[i] ?? string.Empty;
            rows.Add(cells);
        }

        return new ParsedFile(headers ?? Array.Empty<string>(), rows);
    }

    private static async Task<ParsedFile> ReadCsvAsync(Stream s, CancellationToken ct)
    {
        using var sr = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string[]? headers = null;
        var rows = new List<IReadOnlyDictionary<string, string>>();
        string? line;
        while ((line = await sr.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = BulkJobText.SplitCsvLine(line);
            if (headers is null)
            {
                headers = fields.Select(f => f.Trim().ToLowerInvariant()).ToArray();
                continue;
            }
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length && i < fields.Count; i++)
                if (!string.IsNullOrWhiteSpace(headers[i]))
                    cells[headers[i]] = fields[i] ?? string.Empty;
            rows.Add(cells);
        }
        return new ParsedFile(headers ?? Array.Empty<string>(), rows);
    }
}
