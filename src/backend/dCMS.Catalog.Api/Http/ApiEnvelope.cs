namespace dCMS.Catalog.Api.Http;

/// <summary>Consistent JSON envelope per project conventions: <c>{ data, meta, error }</c>.</summary>
public static class ApiEnvelope
{
    public static IResult Ok(object? data = null, object? meta = null) =>
        Results.Json(new { data, meta, error = (object?)null });

    public static IResult Error(string code, string message, int statusCode) =>
        Results.Json(new { data = (object?)null, meta = (object?)null, error = new { code, message } },
            statusCode: statusCode);
}
