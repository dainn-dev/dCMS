namespace dCMS.Core.Messaging;

// DAI-726: cross-service integration contract — Order publishes whenever a Return transitions
// (Pending → Approved/Rejected → Completed). Subscribers: backoffice UI refresh, audit log.

public sealed record ReturnStatusChangedV1(
    string ReturnId,
    string OrderId,
    string FromStatus,
    string ToStatus,
    DateTimeOffset OccurredAt);
