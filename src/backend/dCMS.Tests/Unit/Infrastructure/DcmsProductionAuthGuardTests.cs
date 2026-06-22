using dCMS.AspNetCore.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace dCMS.Tests.Unit.Infrastructure;

public sealed class DcmsProductionAuthGuardTests
{
    [Fact]
    public void EnsureProductionAuthGuard_throws_when_production_and_auth_disabled()
    {
        var configuration = BuildConfig("false");
        var env = new StubHostEnvironment("Production");

        var act = () => configuration.EnsureProductionAuthGuard(env);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Auth:Enabled=false*Production*");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void EnsureProductionAuthGuard_is_noop_outside_production(string environmentName)
    {
        var configuration = BuildConfig("false");
        var env = new StubHostEnvironment(environmentName);

        var act = () => configuration.EnsureProductionAuthGuard(env);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureProductionAuthGuard_is_noop_when_auth_enabled_in_production()
    {
        var configuration = BuildConfig("true");
        var env = new StubHostEnvironment("Production");

        var act = () => configuration.EnsureProductionAuthGuard(env);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureProductionAuthGuard_is_noop_when_auth_key_absent_in_production()
    {
        // Default is true when key is absent — no exception expected.
        var configuration = new ConfigurationBuilder().Build();
        var env = new StubHostEnvironment("Production");

        var act = () => configuration.EnsureProductionAuthGuard(env);

        act.Should().NotThrow();
    }

    private static IConfiguration BuildConfig(string authEnabledValue) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Enabled"] = authEnabledValue })
            .Build();

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
