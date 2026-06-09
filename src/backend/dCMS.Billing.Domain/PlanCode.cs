namespace dCMS.Billing.Domain;

public enum PlanCode
{
    Starter = 0,
    Pro = 1,
    Enterprise = 2,
}

public static class PlanCodeExtensions
{
    public static string ToPersistedValue(this PlanCode code) =>
        code switch
        {
            PlanCode.Starter => "starter",
            PlanCode.Pro => "pro",
            PlanCode.Enterprise => "enterprise",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
        };

    public static PlanCode ParsePersisted(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "starter" => PlanCode.Starter,
            "pro" => PlanCode.Pro,
            "enterprise" => PlanCode.Enterprise,
            _ => throw new ArgumentException($"Unknown plan code '{value}'.", nameof(value)),
        };
}
