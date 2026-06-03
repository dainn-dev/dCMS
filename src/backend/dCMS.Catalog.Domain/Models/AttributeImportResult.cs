namespace dCMS.Core.Models;

/// <summary>Single row from attribute values bulk import (DAI-597).</summary>
public sealed record AttributeImportRowInput(
    string AttributeCode,
    IReadOnlyList<string> Values,
    string Action = "Merge");

public sealed record AttributeImportRowResult(string AttributeCode, string Status, string? Message);

public sealed record AttributeImportResult(
    int Imported,
    int Skipped,
    IReadOnlyList<AttributeImportRowResult> Rows);
