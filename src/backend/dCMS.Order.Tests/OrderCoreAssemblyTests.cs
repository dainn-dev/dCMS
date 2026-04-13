using dCMS.Order.Core;

namespace dCMS.Order.Tests;

public sealed class OrderCoreAssemblyTests
{
    [Fact]
    public void OrderAssembly_name_is_expected() =>
        Assert.Equal("dCMS.Order.Core", OrderAssembly.Name);
}
