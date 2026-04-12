namespace dCMS.Core.Models;

/// <summary>Tenant-scoped category row for backoffice wizard / tree UIs (US-13).</summary>
public sealed record CatalogCategoryRow(int Id, int? ParentId, string Name, string Slug, string Path, int Depth, int SortOrder);
