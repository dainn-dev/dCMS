using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace dCMS.Analyzers;

/// <summary>
/// DAI-702 — Warns when a Dapper query (QueryAsync / ExecuteAsync / QueryFirst… / QuerySingle…)
/// targets a tenant-scoped table but the literal SQL string omits a tenant filter.
///
/// Detection is intentionally conservative:
///   - Only inspects when the first argument is a literal string (or interpolated/concatenated of literals).
///   - Only matches when the SQL contains <c>FROM &lt;known_table&gt;</c> for a configured table.
///   - Skips invocations whose containing method (or class) has [CrossTenantAllowed].
///
/// Tables list is loaded from <c>dcms.analyzers.json</c> as an additional file; falls back to a sane default.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TenantScopeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DCMS001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Dapper query missing tenant scope",
        messageFormat: "Dapper query against tenant-scoped table '{0}' has no tenant_id filter; add it or annotate with [CrossTenantAllowed]",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Every Dapper query that touches a tenant-scoped table must filter by tenant_id (or join through a tenant-scoped parent). DAI-682 / DAI-702.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    private static readonly string[] DapperMethodNames =
    {
        "QueryAsync", "ExecuteAsync", "QueryFirstAsync", "QueryFirstOrDefaultAsync",
        "QuerySingleAsync", "QuerySingleOrDefaultAsync", "ExecuteScalarAsync",
        "Query", "Execute", "QueryFirst", "QueryFirstOrDefault",
        "QuerySingle", "QuerySingleOrDefault", "ExecuteScalar",
    };

    private static readonly ImmutableHashSet<string> DefaultTenantScopedTables = ImmutableHashSet.Create(
        System.StringComparer.OrdinalIgnoreCase,
        // PostgreSQL quoted PascalCase (Order/Catalog/Promotion services)
        "Orders", "OrderItems", "OrderReturns", "OrderReturnItems", "OrderPromotions",
        "Products", "ProductVariants", "ProductImages", "Categories", "Brands",
        "Campaigns", "PromoCodes", "PromotionRedemptions",
        "Stock", "StockMovements", "Warehouses",
        // snake_case fallback (any future tables / Umbraco-side dcms_* tables already excluded)
        "orders", "order_items", "order_returns", "order_return_items",
        "products", "product_variants", "product_images", "categories", "brands",
        "campaigns", "promo_codes", "promotion_redemptions",
        "stock", "stock_movements", "warehouses");

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var tables = LoadTablesFromAdditionalFiles(start.Options) ?? DefaultTenantScopedTables;
            start.RegisterSyntaxNodeAction(ctx => Analyze(ctx, tables), SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext ctx, ImmutableHashSet<string> tables)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax member) return;
        var name = member.Name.Identifier.ValueText;
        if (System.Array.IndexOf(DapperMethodNames, name) < 0) return;

        // Skip if the enclosing method or type is annotated [CrossTenantAllowed].
        if (HasCrossTenantAllowed(invocation, ctx.SemanticModel, ctx.CancellationToken))
            return;

        var firstArg = invocation.ArgumentList.Arguments.FirstOrDefault();
        if (firstArg is null) return;

        // Dapper convenience: many call sites wrap SQL in `new CommandDefinition(sql, ...)`.
        // Peek through to the inner string argument when present.
        var sqlExpr = firstArg.Expression;
        if (sqlExpr is ObjectCreationExpressionSyntax oce && oce.Type.ToString().EndsWith("CommandDefinition"))
        {
            var inner = oce.ArgumentList?.Arguments.FirstOrDefault();
            if (inner is null) return;
            sqlExpr = inner.Expression;
        }

        if (!TryGetLiteralSql(sqlExpr, ctx.SemanticModel, ctx.CancellationToken, out var sql))
            return;

        var table = FindTenantTable(sql, tables);
        if (table is null) return;

        if (HasTenantFilter(sql)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, firstArg.GetLocation(), table));
    }

    private static bool HasCrossTenantAllowed(SyntaxNode node, SemanticModel model, System.Threading.CancellationToken ct)
    {
        for (var n = node.Parent; n != null; n = n.Parent)
        {
            if (n is BaseMethodDeclarationSyntax mds && AttributesContain(mds.AttributeLists, "CrossTenantAllowed")) return true;
            if (n is TypeDeclarationSyntax tds && AttributesContain(tds.AttributeLists, "CrossTenantAllowed")) return true;
        }
        return false;
    }

    private static bool AttributesContain(SyntaxList<AttributeListSyntax> lists, string name)
    {
        foreach (var list in lists)
            foreach (var attr in list.Attributes)
            {
                var n = attr.Name.ToString();
                if (n == name || n.EndsWith("." + name) || n == name + "Attribute" || n.EndsWith("." + name + "Attribute"))
                    return true;
            }
        return false;
    }

    private static bool TryGetLiteralSql(ExpressionSyntax expr, SemanticModel model, System.Threading.CancellationToken ct, out string sql)
    {
        sql = string.Empty;
        switch (expr)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                sql = lit.Token.ValueText;
                return true;
            case InterpolatedStringExpressionSyntax interp:
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var part in interp.Contents)
                    {
                        if (part is InterpolatedStringTextSyntax txt)
                            sb.Append(txt.TextToken.ValueText);
                        else
                            // Non-literal interpolation — bail; we can't reason safely.
                            return false;
                    }
                    sql = sb.ToString();
                    return true;
                }
            default:
                {
                    var c = model.GetConstantValue(expr, ct);
                    if (c.HasValue && c.Value is string s) { sql = s; return true; }
                    return false;
                }
        }
    }

    private static readonly Regex FromTablePattern = new(
        @"\bFROM\s+(?:""([A-Za-z_][A-Za-z0-9_]*)""|\[([A-Za-z_][A-Za-z0-9_]*)\]|([A-Za-z_][A-Za-z0-9_]*))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string? FindTenantTable(string sql, ImmutableHashSet<string> tables)
    {
        foreach (Match m in FromTablePattern.Matches(sql))
        {
            var t = m.Groups[1].Value;
            if (string.IsNullOrEmpty(t)) t = m.Groups[2].Value;
            if (string.IsNullOrEmpty(t)) t = m.Groups[3].Value;
            if (!string.IsNullOrEmpty(t) && tables.Contains(t)) return t;
        }
        return null;
    }

    private static readonly Regex TenantFilterPattern = new(
        @"(?:""TenantId""|\btenant_id\b|\bTenantId\b)\s*(?:=|IN\b|@|:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Some writes parameterize tenant via inline VALUES (... @TenantId, ...) without a WHERE clause.
    private static readonly Regex TenantParamPattern = new(
        @"@TenantId\b",
        RegexOptions.Compiled);

    private static bool HasTenantFilter(string sql)
        => TenantFilterPattern.IsMatch(sql) || TenantParamPattern.IsMatch(sql);

    private static ImmutableHashSet<string>? LoadTablesFromAdditionalFiles(AnalyzerOptions options)
    {
        foreach (var file in options.AdditionalFiles)
        {
            if (!file.Path.EndsWith("dcms.analyzers.json", System.StringComparison.OrdinalIgnoreCase)) continue;
            var text = file.GetText()?.ToString();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Tiny hand-roll parser — avoid adding System.Text.Json to keep analyzer asm small.
            // Looks for: "tenantScopedTables" : [ "a", "b", ... ]
            var match = Regex.Match(text!, "\"tenantScopedTables\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            var body = match.Groups["body"].Value;
            var names = Regex.Matches(body, "\"([^\"]+)\"")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(s => !string.IsNullOrWhiteSpace(s));
            return ImmutableHashSet.CreateRange(System.StringComparer.OrdinalIgnoreCase, names);
        }
        return null;
    }
}
