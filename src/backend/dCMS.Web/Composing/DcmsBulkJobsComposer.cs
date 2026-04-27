using dCMS.Core.Persistence;
using dCMS.Infrastructure.Catalog;
using dCMS.Web.BulkJobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace dCMS.Web.Composing;

public sealed class DcmsBulkJobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        var umbracoCs = builder.Config.GetConnectionString("umbracoDbDSN");
        if (string.IsNullOrWhiteSpace(umbracoCs))
            throw new InvalidOperationException("ConnectionStrings:umbracoDbDSN is required for Hangfire.");

        builder.Services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(umbracoCs, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.FromSeconds(10),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
            }));

        builder.Services.AddHangfireServer(o =>
        {
            o.Queues = ["bulk", "default"];
            o.WorkerCount = Math.Max(1, Math.Min(8, Environment.ProcessorCount));
        });

        var catalogCs = builder.Config.GetConnectionString("Catalog");
        if (!string.IsNullOrWhiteSpace(catalogCs))
        {
            builder.Services.TryAddSingleton<ICatalogPersistence>(_ => new SqlCatalogPersistence(catalogCs!));
            builder.Services.AddScoped<CatalogBulkJobRunner>();
        }

        builder.Services.AddScoped<IBulkJobRepository, SqlBulkJobRepository>();
        builder.Services.AddScoped<OrdersBulkJobRunner>();

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter("DcmsBulkJobsHangfire")
            {
                Endpoints = app => app.UseHangfireDashboard(
                    "/umbraco/dcms/hangfire",
                    new DashboardOptions
                    {
                        Authorization = [new DcmsBackOfficeHangfireAuthorizationFilter()],
                        IgnoreAntiforgeryToken = true,
                    }),
            });
        });
    }
}
