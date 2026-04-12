namespace dCMS.Inventory.Api.Http;

public static class ApiEnvelope
{
    public static IResult Ok(object? data = null, object? meta = null) =>
        Results.Json(new { data, meta, error = (object?)null });

    public static IResult Error(string code, string message, int statusCode, object? meta = null) =>
        Results.Json(new { data = (object?)null, meta, error = new { code, message } }, statusCode: statusCode);
}
