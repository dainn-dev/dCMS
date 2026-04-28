namespace dCMS.Catalog.Worker.Imports;

public interface IImportRowProcessor
{
    string Type { get; }
    Task<RowResult> ProcessAsync(ImportRow row, ImportContext ctx, CancellationToken ct);
}

public readonly record struct RowResult(bool IsError, string? Message)
{
    public static readonly RowResult Ok = new(false, null);
    public static RowResult Err(string m) => new(true, m);
}

public readonly record struct ImportContext(string TenantId, string JobId);

public sealed record ImportRow(int Index, string Key, IReadOnlyDictionary<string, string> Cells);
