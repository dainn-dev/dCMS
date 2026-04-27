namespace dCMS.Web.BulkJobs;

public static class BulkJobFileStorage
{
    public static string GetRootPath(string contentRoot) =>
        Path.Combine(contentRoot, "umbraco", "Data", "BulkJobFiles");

    public static string GetJobDir(string contentRoot, string tenantId, Guid jobId) =>
        Path.Combine(GetRootPath(contentRoot), tenantId, jobId.ToString("N"));

    public static string GetInputFilePath(string contentRoot, string tenantId, Guid jobId) =>
        Path.Combine(GetJobDir(contentRoot, tenantId, jobId), "input.csv");

    public static string GetOutputFilePath(string contentRoot, string tenantId, Guid jobId) =>
        Path.Combine(GetJobDir(contentRoot, tenantId, jobId), "export.csv");

    public static void EnsureJobDir(string contentRoot, string tenantId, Guid jobId) =>
        Directory.CreateDirectory(GetJobDir(contentRoot, tenantId, jobId));

    public static string GetInputRelativeKey(string tenantId, Guid jobId) =>
        Path.Combine("umbraco", "Data", "BulkJobFiles", tenantId, jobId.ToString("N"), "input.csv");

    public static string GetOrdersInputRelativeKey(string tenantId, Guid jobId) =>
        Path.Combine("umbraco", "Data", "BulkJobFiles", tenantId, jobId.ToString("N"), "input.json");
}
