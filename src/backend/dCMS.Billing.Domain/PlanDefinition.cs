namespace dCMS.Billing.Domain;

public sealed class PlanDefinition
{
    public required string Id { get; init; }
    public required PlanCode Code { get; init; }
    public required string Name { get; init; }
    public required int MaxBrands { get; init; }
    public required int MaxActiveProducts { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public required bool IsActive { get; init; }
}
