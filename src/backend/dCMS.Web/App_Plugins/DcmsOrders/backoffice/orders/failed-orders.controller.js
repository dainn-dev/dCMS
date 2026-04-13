(function () {
    "use strict";

    function DcmsFailedOrdersController($scope, $sce, $window) {
        var vm = this;

        vm.orderDetailPartialUrl = $sce.trustAsResourceUrl(
            "/App_Plugins/DcmsOrders/backoffice/orders/order-detail.partial.html"
        );

        /* ── Filters ───────────────────────────────────────────── */
        vm.filters = {
            doNumber:         "",
            orderNumber:      "",
            orderType:        "",
            failureReason:    "",
            orderDateFrom:    "2026-03-31",
            orderDateTo:      "2026-04-14",
            failureDateFrom:  "",
            failureDateTo:    "",
            retryStatus:      "",
            shippingStatus:   "",
            customerName:     "",
            contactNo:        "",
            email:            "",
            processedBy:      "",
            paymentMethod:    "",
            storeName:        "",
            affiliateId:      "",
            tags:             "",
        };

        vm.activeFilterCount = 0;
        $scope.$watch("vm.filters", function (val) {
            var n = 0;
            Object.keys(val).forEach(function (k) { if (val[k] !== "" && val[k] !== null) n++; });
            vm.activeFilterCount = n;
        }, true);

        /* ── Sample data ───────────────────────────────────────── */
        vm.allOrders = [
            { doNumber: "B02B504020023", orderNumber: "0026402023", orderDate: "22-Apr-26 07:45", type: "delivery",   customerName: "Tan Wei Liang",  contactNo: "+6591234500", email: "weilian.tan@gmail.com",     failureDate: "22-Apr-26 08:01", failureReason: "Payment Failed",      retryStatus: "failed",   retryStatusLabel: "Failed",   shippingStatus: "Failed",  processedBy: "tangs_content_cdm", tags: "",       store: "tangs_orchard",  paymentMethod: "Credit Card",  affiliateId: "",       identityNo: "S8812345A" },
            { doNumber: "B02B504020026", orderNumber: "0026402026", orderDate: "22-Apr-26 08:00", type: "collection", customerName: "Ng Siew Ling",   contactNo: "+6598765400", email: "siewling.ng@hotmail.com",   failureDate: "22-Apr-26 08:15", failureReason: "Validation Error",    retryStatus: "retrying", retryStatusLabel: "Retrying", shippingStatus: "Pending", processedBy: "tangs_content_cdm", tags: "",       store: "tangs_vivocity", paymentMethod: "PayNow",       affiliateId: "",       identityNo: "S9023456B" },
            { doNumber: "B02B504020029", orderNumber: "0026402029", orderDate: "22-Apr-26 08:30", type: "delivery",   customerName: "Rajesh Kumar",   contactNo: "+6587654300", email: "rajesh.k@techfirm.sg",     failureDate: "22-Apr-26 08:45", failureReason: "Stock Unavailable",   retryStatus: "pending",  retryStatusLabel: "Pending",  shippingStatus: "Open",    processedBy: "tangs_ops_cdm",     tags: "vip",    store: "tangs_online",   paymentMethod: "GrabPay",      affiliateId: "AFF001", identityNo: "G8934567C" },
            { doNumber: "B02B504020032", orderNumber: "0026402032", orderDate: "22-Apr-26 09:10", type: "express",    customerName: "Lim Hui Fen",    contactNo: "+6512345600", email: "huifen.lim@icloud.com",     failureDate: "22-Apr-26 09:12", failureReason: "Timeout",             retryStatus: "retrying", retryStatusLabel: "Retrying", shippingStatus: "Pending", processedBy: "tangs_content_cdm", tags: "",       store: "tangs_orchard",  paymentMethod: "Apple Pay",    affiliateId: "",       identityNo: "S9234567D" },
            { doNumber: "B02B504020035", orderNumber: "0026402035", orderDate: "22-Apr-26 09:45", type: "delivery",   customerName: "Chan Ah Beng",   contactNo: "+6565432100", email: "ahbeng.chan@yahoo.com",     failureDate: "22-Apr-26 10:00", failureReason: "Payment Failed",      retryStatus: "resolved", retryStatusLabel: "Resolved", shippingStatus: "Open",    processedBy: "tangs_content_cdm", tags: "",       store: "tangs_vivocity", paymentMethod: "NETS",         affiliateId: "",       identityNo: "S7823456E" },
            { doNumber: "B02B504020038", orderNumber: "0026402038", orderDate: "22-Apr-26 10:30", type: "delivery",   customerName: "Preethi Nair",   contactNo: "+6578901200", email: "preethi.n@gmail.com",       failureDate: "22-Apr-26 10:35", failureReason: "System Error",        retryStatus: "failed",   retryStatusLabel: "Failed",   shippingStatus: "Failed",  processedBy: "tangs_ops_cdm",     tags: "member", store: "tangs_app",      paymentMethod: "Credit Card",  affiliateId: "AFF002", identityNo: "F9034567F" },
            { doNumber: "B02B504020041", orderNumber: "0026402041", orderDate: "22-Apr-26 11:00", type: "collection", customerName: "Wong Boon Huat", contactNo: "+6545678900", email: "boonhuat.w@singtel.com",    failureDate: "22-Apr-26 11:05", failureReason: "Validation Error",    retryStatus: "pending",  retryStatusLabel: "Pending",  shippingStatus: "Pending", processedBy: "tangs_content_cdm", tags: "",       store: "tangs_orchard",  paymentMethod: "PayNow",       affiliateId: "",       identityNo: "S8512345G" },
            { doNumber: "B02B504020044", orderNumber: "0026402044", orderDate: "22-Apr-26 11:30", type: "delivery",   customerName: "Siti Rahimah",   contactNo: "+6534567800", email: "siti.r@gmail.com",          failureDate: "22-Apr-26 11:42", failureReason: "Payment Failed",      retryStatus: "retrying", retryStatusLabel: "Retrying", shippingStatus: "Pending", processedBy: "tangs_content_cdm", tags: "",       store: "tangs_online",   paymentMethod: "GrabPay",      affiliateId: "AFF003", identityNo: "S9112345H" },
            { doNumber: "B02B504020047", orderNumber: "0026402047", orderDate: "22-Apr-26 12:15", type: "express",    customerName: "Kevin Loh",      contactNo: "+6523456700", email: "kevin.loh@outlook.sg",      failureDate: "22-Apr-26 12:17", failureReason: "Timeout",             retryStatus: "resolved", retryStatusLabel: "Resolved", shippingStatus: "Open",    processedBy: "tangs_ops_cdm",     tags: "vip",    store: "tangs_vivocity", paymentMethod: "Apple Pay",    affiliateId: "",       identityNo: "S8723456J" },
            { doNumber: "B02B504020050", orderNumber: "0026402050", orderDate: "22-Apr-26 12:45", type: "delivery",   customerName: "Mary Ong",       contactNo: "+6556789000", email: "mary.ong@yahoo.com.sg",     failureDate: "22-Apr-26 13:00", failureReason: "Stock Unavailable",   retryStatus: "failed",   retryStatusLabel: "Failed",   shippingStatus: "Failed",  processedBy: "tangs_content_cdm", tags: "",       store: "tangs_app",      paymentMethod: "NETS",         affiliateId: "",       identityNo: "S7634567K" },
            { doNumber: "B02B504020053", orderNumber: "0026402053", orderDate: "22-Apr-26 13:30", type: "delivery",   customerName: "Ali Hassan",     contactNo: "+6512398700", email: "ali.hassan@gmail.com",      failureDate: "22-Apr-26 13:45", failureReason: "System Error",        retryStatus: "pending",  retryStatusLabel: "Pending",  shippingStatus: "Pending", processedBy: "tangs_ops_cdm",     tags: "member", store: "tangs_orchard",  paymentMethod: "Credit Card",  affiliateId: "AFF001", identityNo: "S9456789L" },
            { doNumber: "B02B504020056", orderNumber: "0026402056", orderDate: "22-Apr-26 14:00", type: "collection", customerName: "Chen Mei Xia",   contactNo: "+6567891200", email: "meixia.chen@company.sg",    failureDate: "22-Apr-26 14:08", failureReason: "Payment Failed",      retryStatus: "retrying", retryStatusLabel: "Retrying", shippingStatus: "Pending", processedBy: "tangs_content_cdm", tags: "",       store: "tangs_online",   paymentMethod: "PayNow",       affiliateId: "",       identityNo: "S8812345M" },
        ];

        /* ── Pagination ────────────────────────────────────────── */
        vm.filteredOrders = vm.allOrders.slice();
        vm.currentPage    = 1;
        vm.pageSize       = 10;
        vm.totalPages     = 1;
        vm.fromRecord     = 1;
        vm.pagedOrders    = [];

        function updatePaging() {
            vm.totalPages  = Math.max(1, Math.ceil(vm.filteredOrders.length / vm.pageSize));
            if (vm.currentPage > vm.totalPages) vm.currentPage = vm.totalPages;
            var start      = (vm.currentPage - 1) * vm.pageSize;
            vm.fromRecord  = vm.filteredOrders.length === 0 ? 0 : start + 1;
            vm.pagedOrders = vm.filteredOrders.slice(start, start + vm.pageSize);
        }

        vm.goPage = function (p) {
            if (p < 1 || p > vm.totalPages) return;
            vm.currentPage = p;
            updatePaging();
        };

        vm.pageRange = function () {
            var pages = [];
            var start = Math.max(1, vm.currentPage - 2);
            var end   = Math.min(vm.totalPages, start + 4);
            for (var i = start; i <= end; i++) pages.push(i);
            return pages;
        };

        /* ── Search / filter ───────────────────────────────────── */
        vm.search = function () {
            var f = vm.filters;
            vm.filteredOrders = vm.allOrders.filter(function (o) {
                var ci = function (a, b) { return a.toLowerCase().indexOf(b.toLowerCase()) !== -1; };
                if (f.doNumber       && !ci(o.doNumber, f.doNumber))               return false;
                if (f.orderNumber    && !ci(o.orderNumber, f.orderNumber))         return false;
                if (f.orderType      && o.type !== f.orderType)                    return false;
                if (f.failureReason  && o.failureReason.toLowerCase().replace(/ /g, "_") !== f.failureReason) return false;
                if (f.retryStatus    && o.retryStatus !== f.retryStatus)           return false;
                if (f.shippingStatus && o.shippingStatus !== f.shippingStatus)     return false;
                if (f.customerName   && !ci(o.customerName, f.customerName))       return false;
                if (f.contactNo      && o.contactNo.indexOf(f.contactNo) === -1)   return false;
                if (f.email          && !ci(o.email, f.email))                     return false;
                if (f.processedBy    && !ci(o.processedBy, f.processedBy))         return false;
                if (f.paymentMethod  && o.paymentMethod !== f.paymentMethod)       return false;
                if (f.storeName      && !ci(o.store, f.storeName))                 return false;
                if (f.affiliateId    && !ci(o.affiliateId, f.affiliateId))         return false;
                if (f.tags           && !ci(o.tags, f.tags))                       return false;
                return true;
            });
            vm.currentPage = 1;
            updatePaging();
        };

        vm.saveFilters  = function () { /* persist to user prefs via API */ };
        vm.export       = function () { /* call export endpoint */ };

        vm.view   = "list";
        vm.detail = null;
        vm.sections = { orderItems: true, remarks: true };
        vm.toggleSection = function (key) { vm.sections[key] = !vm.sections[key]; };

        vm.backToList = function () {
            vm.view   = "list";
            vm.detail = null;
        };

        vm.currency = function (n, display) {
            if (display != null && String(display).trim() !== "") return display;
            if (n == null || n === "" || (typeof n === "number" && isNaN(n))) return "—";
            return "S$ " + Number(n).toFixed(2);
        };
        vm.decimal = function (n, display) {
            if (display != null && String(display).trim() !== "") return display;
            if (n == null || n === "" || (typeof n === "number" && isNaN(n))) return "—";
            return Number(n).toFixed(2);
        };

        vm.viewOrder = function (order) {
            vm.detail = angular.copy(order);
            vm.detail.doNumber = order.doNumber || order.spNumber || "";
            vm.detail.items = [];

            // Sample order items — replace with Order.Api call
            vm.detail.orderItems = [
                {
                    brand: "AIMER MEN",
                    upc: "00000TEST0002",
                    sku: "ABC123",
                    name: "Test Product 2 test",
                    attributes: ["AOX COLOR: Grey", "SABRINAGOH Size: FREE SIZE"],
                    discountLabel: "REBATES (S$3.00)",
                    stock: 88,
                    qty: 1,
                    unitPrice: 19.00,
                    discount: 3.00,
                    extraFees: 0.00,
                    total: 16.00,
                    status: "open",
                    tag: "",
                },
            ];

            vm.detail.remarks = [
                {
                    text: "Change Order Details Info<br/>&bull; Changed RebateEarned From 0.0000 to 1.0",
                    addedBy: "tangs_system_user_uat",
                    addedOn: "02/04/2026 5:57:27 PM",
                },
            ];

            vm.sections = { orderItems: true, remarks: true };
            vm.view = "detail";
            $window.scrollTo(0, 0);
        };

        updatePaging();
    }

    DcmsFailedOrdersController.$inject = ["$scope", "$sce", "$window"];
    angular.module("umbraco").controller("DcmsFailedOrdersController", DcmsFailedOrdersController);
})();
