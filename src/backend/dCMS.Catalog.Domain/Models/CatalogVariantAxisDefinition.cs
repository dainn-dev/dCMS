namespace dCMS.Core.Models;

/// <summary>One variant axis (attribute) with pickable values for the Umbraco wizard (DAI-284).</summary>
public sealed record CatalogVariantAxisValue(int Id, string Name, int SortOrder);

public sealed record CatalogVariantAxisDefinition(int AttributeId, string Name, IReadOnlyList<CatalogVariantAxisValue> Values);
