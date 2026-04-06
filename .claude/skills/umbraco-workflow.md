# Umbraco Workflow Skill

Dùng khi làm việc với Umbraco content types, backoffice customization, hoặc Content Delivery API.

## Content Types

Umbraco content types được define bằng code-first (Umbraco.Cms.Core attributes) hoặc qua backoffice UI.

**Preferred approach: Code-first** để version control:
```csharp
[ContentType("product", "Product")]
public class ProductContentType : PublishedContentModel
{
    public ProductContentType(IPublishedContent content, IPublishedValueFallback fallback)
        : base(content, fallback) { }

    [ContentProperty("productName")]
    public string ProductName => this.Value<string>(fallback, "productName");

    [ContentProperty("price")]
    public decimal Price => this.Value<decimal>(fallback, "price");
}
```

## Content Delivery API

Umbraco Content Delivery API (v2) endpoints:
```
GET /umbraco/delivery/api/v2/content              # List content
GET /umbraco/delivery/api/v2/content/{id}         # Get by ID
GET /umbraco/delivery/api/v2/content?filter=...   # Filter
```

**Config trong `appsettings.json`:**
```json
{
  "Umbraco": {
    "CMS": {
      "DeliveryApi": {
        "Enabled": true,
        "PublicAccess": false,
        "ApiKey": "your-api-key"
      }
    }
  }
}
```

## Custom API Controllers

Extend Umbraco với custom API controllers (không dùng Razor):
```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQueryDto query)
    {
        var result = await _productService.GetProductsAsync(query);
        return Ok(new { data = result.Items, meta = result.Meta });
    }
}
```

## Multi-tenant Setup

Mỗi tenant có Umbraco instance riêng. Tenant được identify qua:
- Subdomain (e.g., `store1.dcms.com`)
- Custom domain (e.g., `mystore.com` → mapped đến tenant)

```csharp
// Middleware để identify tenant từ request
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
    {
        var tenant = await resolver.ResolveAsync(context.Request.Host.Host);
        context.Items["TenantId"] = tenant.Id;
        await _next(context);
    }
}
```

## Workflow khi thêm feature mới

1. Define content type (code-first)
2. Create migration nếu cần schema change
3. Implement service trong `dCMS.Core/`
4. Implement repository trong `dCMS.Infrastructure/`
5. Register DI trong `dCMS.Web/Program.cs`
6. Expose API controller
7. Test với xUnit
8. Update Elasticsearch index nếu liên quan đến search
