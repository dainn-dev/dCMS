using dCMS.Order.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace dCMS.Order.Tests;

public sealed class OrderInfrastructureRegistrationTests
{
    [Fact]
    public void AddOrderInfrastructure_can_build_service_provider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Order"] = "Host=127.0.0.1;Port=65432;Database=none;Username=x;Password=x",
            ["RabbitMq:Host"] = "127.0.0.1",
            ["RabbitMq:User"] = "guest",
            ["RabbitMq:Pass"] = "guest",
            ["Inventory:BaseUrl"] = "http://127.0.0.1:59999/",
            ["Payment:BaseUrl"] = "http://127.0.0.1:59998/",
        }!).Build();

        services.AddOrderInfrastructure(config);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
    }
}
