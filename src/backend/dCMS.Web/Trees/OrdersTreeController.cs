using dCMS.Web.Sections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Trees;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Trees;
using Umbraco.Cms.Web.BackOffice.Trees;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Extensions;

namespace dCMS.Web.Trees;

/// <summary>Orders navigation tree for the <see cref="DcmsOrdersSection"/> (top header ribbon).</summary>
[Tree(
    DcmsOrdersSection.SectionAlias,
    "orders",
    TreeTitle = "Orders",
    TreeGroup = "orders",
    SortOrder = 0)]
[PluginController("DcmsOrders")]
public sealed class OrdersTreeController : TreeController
{
    private const string OrdersTreeAlias = "orders";

    private const string NodeOrderProcessing = "order-processing";
    private const string NodeFailedOrders = "failed-orders";
    private const string NodeRefundCases = "refund-cases";

    private readonly IMenuItemCollectionFactory _menuItemCollectionFactory;

    public OrdersTreeController(
        ILocalizedTextService localizedTextService,
        UmbracoApiControllerTypeCollection umbracoApiControllerTypeCollection,
        IMenuItemCollectionFactory menuItemCollectionFactory,
        IEventAggregator eventAggregator)
        : base(localizedTextService, umbracoApiControllerTypeCollection, eventAggregator)
    {
        _menuItemCollectionFactory = menuItemCollectionFactory ?? throw new ArgumentNullException(nameof(menuItemCollectionFactory));
    }

    protected override ActionResult<TreeNodeCollection> GetTreeNodes(string id, FormCollection queryStrings)
    {
        var nodes = new TreeNodeCollection();
        if (id != Constants.System.Root.ToInvariantString())
            return nodes;

        AddLeaf(nodes, queryStrings, NodeOrderProcessing, "Order processing", "icon-box");
        AddLeaf(nodes, queryStrings, NodeFailedOrders, "Failed Orders", "icon-alert");
        AddLeaf(nodes, queryStrings, NodeRefundCases, "Refund Cases", "icon-credit-card");
        return nodes;
    }

    private void AddLeaf(TreeNodeCollection nodes, FormCollection queryStrings, string nodeId, string title, string icon)
    {
        var node = CreateTreeNode(nodeId, Constants.System.Root.ToInvariantString(), queryStrings, title, icon, false);
        node.RoutePath = $"{DcmsOrdersSection.SectionAlias}/{OrdersTreeAlias}/{nodeId}";
        nodes.Add(node);
    }

    protected override ActionResult<MenuItemCollection> GetMenuForNode(string id, FormCollection queryStrings)
    {
        var menu = _menuItemCollectionFactory.Create();
        menu.Items.Add(new RefreshNode(LocalizedTextService, true));
        return menu;
    }

    protected override ActionResult<TreeNode?> CreateRootNode(FormCollection queryStrings)
    {
        var rootResult = base.CreateRootNode(queryStrings);
        if (rootResult.Result is not null)
            return rootResult;

        var root = rootResult.Value ?? throw new InvalidOperationException("Orders tree root is null.");
        root.Icon = "icon-shopping-basket";
        root.HasChildren = true;
        root.MenuUrl = null;
        return root;
    }
}
