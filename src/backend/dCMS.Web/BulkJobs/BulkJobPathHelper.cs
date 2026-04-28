namespace dCMS.Web.BulkJobs;

internal static class BulkJobPathHelper
{
    public static string ResolveUnderContentRoot(IWebHostEnvironment env, string? blobRef)
    {
        if (string.IsNullOrWhiteSpace(blobRef)) throw new InvalidOperationException("Blob ref is required.");
        if (Path.IsPathRooted(blobRef)) return blobRef;
        return Path.GetFullPath(Path.Combine(env.ContentRootPath, blobRef));
    }
}
