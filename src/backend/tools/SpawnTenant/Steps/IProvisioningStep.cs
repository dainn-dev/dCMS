namespace dCMS.Tools.SpawnTenant.Steps;

/// <summary>
/// Contract for a single idempotent provisioning step.
/// Steps may be re-run — if already succeeded (checkpoint says "completed"),
/// ExecuteAsync should return immediately without side effects.
/// </summary>
public interface IProvisioningStep
{
    /// <summary>Step name matching ProvisioningSteps.StepName, e.g. "validate_request".</summary>
    string Name { get; }

    /// <summary>Execution order (1-based).</summary>
    int Order { get; }

    /// <summary>Maximum retry attempts before marking as failed.</summary>
    int MaxRetries { get; }

    /// <summary>
    /// Execute the step. If the step has already succeeded (ctx.IsStepCompleted(Name)),
    /// the implementation should return immediately.
    /// On success, call ctx.MarkStepCompleted(Name).
    /// </summary>
    Task ExecuteAsync(ProvisioningContext ctx);

    /// <summary>
    /// Reverse the step's effects. Only called during rollback.
    /// Implementations should be idempotent and tolerate partially-rolled-back state.
    /// </summary>
    Task RollbackAsync(ProvisioningContext ctx);
}
