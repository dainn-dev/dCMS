namespace dCMS.Persistence.Ef.Catalog;

public sealed class CatalogCategoryEntity
{
    public int Id { get; set; }
    public string TenantId { get; set; } = null!;
    public int? ParentId { get; set; }
    public string TreePath { get; set; } = "/";
    public int Depth { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int SortOrder { get; set; }
}
