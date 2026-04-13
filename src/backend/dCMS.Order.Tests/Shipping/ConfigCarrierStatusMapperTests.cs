using dCMS.Order.Infrastructure.Shipping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace dCMS.Order.Tests.Shipping;

public sealed class ConfigCarrierStatusMapperTests
{
    [Theory]
    [InlineData("GHN", "DELIVERED", MappedStatus.Delivered)]
    [InlineData("GHTK", "DELIVERED", MappedStatus.Delivered)]
    [InlineData("ViettelPost", "DA_GIAO", MappedStatus.Delivered)]
    public void Map_legacy_keys_uses_config_per_carrier(string carrier, string external, MappedStatus expected)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"Shipment:Carriers:GHN:StatusMap:DELIVERED"] = "delivered",
            [$"Shipment:Carriers:GHTK:StatusMap:DELIVERED"] = "delivered",
            [$"Shipment:Carriers:ViettelPost:StatusMap:DA_GIAO"] = "delivered",
        }).Build();

        var mapper = new ConfigCarrierStatusMapper(cfg, NullLogger<ConfigCarrierStatusMapper>.Instance);
        Assert.Equal(expected, mapper.Map(carrier, external));
    }

    [Fact]
    public void Map_nested_section_supports_config_driven_json_shape()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Shipment:CarrierStatusMapping:GHN:Delivered"] = "delivered",
            ["Shipment:CarrierStatusMapping:GHN:IN_TRANSIT"] = "in_transit",
        }).Build();

        var mapper = new ConfigCarrierStatusMapper(cfg, NullLogger<ConfigCarrierStatusMapper>.Instance);
        Assert.Equal(MappedStatus.Delivered, mapper.Map("GHN", "Delivered"));
        Assert.Equal(MappedStatus.InTransit, mapper.Map("GHN", "IN_TRANSIT"));
    }

    [Fact]
    public void Map_unknown_returns_Unknown_and_does_not_throw()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var mapper = new ConfigCarrierStatusMapper(cfg, NullLogger<ConfigCarrierStatusMapper>.Instance);
        Assert.Equal(MappedStatus.Unknown, mapper.Map("GHN", "SOMETHING_NEW"));
    }
}

