using dCMS.Core.Approvals;

namespace dCMS.Approval.Api.Routes;

public sealed class ApprovalSubjectRegistry(IEnumerable<IApprovalSubject> subjects)
{
    private readonly Dictionary<string, IApprovalSubject> _byEntityType =
        subjects.ToDictionary(x => x.EntityType.Trim(), StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string entityType, out IApprovalSubject subject) =>
        _byEntityType.TryGetValue(entityType.Trim(), out subject!);
}

