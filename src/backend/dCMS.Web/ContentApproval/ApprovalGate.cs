namespace dCMS.Web.ContentApproval;

/// <summary>
/// DAI-721: AsyncLocal flag that lets the internal approval-callback publish bypass the
/// <see cref="ContentPublishingApprovalHandler"/> guard (otherwise the publish would loop:
/// approval → publish → handler cancels → submit again).
/// </summary>
internal static class ApprovalGate
{
    private static readonly AsyncLocal<bool> _bypass = new();

    public static bool IsBypassed => _bypass.Value;

    public static IDisposable Bypass()
    {
        _bypass.Value = true;
        return new Resetter();
    }

    private sealed class Resetter : IDisposable
    {
        public void Dispose() => _bypass.Value = false;
    }
}
