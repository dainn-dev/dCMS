using System.Runtime.CompilerServices;
using System.Text;
using ClosedXML.Excel;

namespace dCMS.Catalog.Worker.Imports;

// DAI-707 — streaming XLSX/CSV row reader for bulk imports.
// Sniffs first 4 bytes: ZIP magic 50 4B 03 04 -> XLSX, otherwise CSV.
// Maps the first non-empty row as headers; downstream rows yield (Index, Key, Cells)
// where Key is sku|code|external_id|"row:{Index}".
public static class XlsxStreamReader
{
    public static async IAsyncEnumerable<ImportRow> EnumerateRowsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var buffered = await BufferAsync(stream, ct).ConfigureAwait(false);
        var head = new byte[4];
        var read = await buffered.ReadAsync(head.AsMemory(0, 4), ct).ConfigureAwait(false);
        buffered.Position = 0;

        bool isXlsx = read >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;
        if (isXlsx)
        {
            foreach (var row in ReadXlsx(buffered))
            {
                ct.ThrowIfCancellationRequested();
                yield return row;
            }
        }
        else
        {
            foreach (var row in ReadCsv(buffered))
            {
                ct.ThrowIfCancellationRequested();
                yield return row;
            }
        }
    }

    private static async Task<MemoryStream> BufferAsync(Stream src, CancellationToken ct)
    {
        if (src is MemoryStream ms && ms.CanSeek) { ms.Position = 0; return ms; }
        var buf = new MemoryStream();
        await src.CopyToAsync(buf, ct).ConfigureAwait(false);
        buf.Position = 0;
        return buf;
    }

    private static IEnumerable<ImportRow> ReadXlsx(Stream s)
    {
        using var wb = new XLWorkbook(s);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) yield break;

        string[]? headers = null;
        int rowIdx = 0;

        foreach (var r in ws.RowsUsed())
        {
            var values = r.Cells(1, r.LastCellUsed()?.Address.ColumnNumber ?? 0)
                .Select(c => c.GetString().Trim())
                .ToArray();
            if (values.Length == 0 || values.All(string.IsNullOrWhiteSpace))
                continue;

            if (headers is null)
            {
                headers = values.Select(v => v.ToLowerInvariant()).ToArray();
                continue;
            }

            rowIdx++;
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length && i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(headers[i]))
                    cells[headers[i]] = values[i] ?? string.Empty;

            yield return new ImportRow(rowIdx, ExtractKey(cells, rowIdx), cells);
        }
    }

    private static IEnumerable<ImportRow> ReadCsv(Stream s)
    {
        using var sr = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string[]? headers = null;
        int rowIdx = 0;
        string? line;
        while ((line = sr.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsv(line);
            if (headers is null)
            {
                headers = fields.Select(f => f.Trim().ToLowerInvariant()).ToArray();
                continue;
            }
            rowIdx++;
            var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length && i < fields.Length; i++)
                if (!string.IsNullOrWhiteSpace(headers[i]))
                    cells[headers[i]] = fields[i] ?? string.Empty;
            yield return new ImportRow(rowIdx, ExtractKey(cells, rowIdx), cells);
        }
    }

    private static string ExtractKey(IReadOnlyDictionary<string, string> cells, int rowIdx)
    {
        if (cells.TryGetValue("sku", out var sku) && !string.IsNullOrWhiteSpace(sku)) return sku.Trim();
        if (cells.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code)) return code.Trim();
        if (cells.TryGetValue("external_id", out var ext) && !string.IsNullOrWhiteSpace(ext)) return ext.Trim();
        return $"row:{rowIdx}";
    }

    private static string[] SplitCsv(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else
            {
                if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else if (ch == '"' && sb.Length == 0) inQuotes = true;
                else sb.Append(ch);
            }
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
