using System.Text.Json.Serialization;

namespace dCMS.Web.BulkJobs;

public sealed class OrdersExportRequest
{
    [JsonPropertyName("dateFrom")]
    public DateOnly DateFrom { get; set; }

    [JsonPropertyName("dateTo")]
    public DateOnly DateTo { get; set; }

    /// <summary>Empty = all stores in tenant.</summary>
    [JsonPropertyName("storeId")]
    public string? StoreId { get; set; }
}
