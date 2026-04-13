namespace dCMS.Order.Core;

/// <summary>Anchor type for assembly reference from tests and infrastructure.</summary>
public static class OrderAssembly
{
    public static string Name => typeof(OrderAssembly).Assembly.GetName().Name!;
}
