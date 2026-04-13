(function () {
    "use strict";

    function DcmsOrderProcessingController($scope, $sce, $window) {
        var vm = this;

        /** SCE-trusted template URL — required for ng-include in strict AngularJS mode */
        vm.orderDetailPartialUrl = $sce.trustAsResourceUrl(
            "/App_Plugins/DcmsOrders/backoffice/orders/order-detail.partial.html"
        );

        /* ── Filters ───────────────────────────────────────────── */
        vm.filters = {
            doNumber:         "",
            orderNumber:      "",
            orderType:        "",
            deliveryOption:   "",
            orderDateFrom:    "2026-03-31",
            orderDateTo:      "2026-04-14",
            deliveryDateFrom: "",
            deliveryDateTo:   "",
            fulfilledOnFrom:  "",
            fulfilledOnTo:    "",
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
            { spNumber: "B02B504020022", orderNumber: "0026402022", orderDate: "22-Apr-26 07:30", type: "delivery",   customerName: "Chan Test",      contactNo: "+6521232323", email: "chan.test@market.digital",    deliveryDate: "22-Apr-26 15:26", deliveryOption: "3pm – 4pm",    orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Open",      processedBy: "tangs_content_cdm", tags: "",          store: "tangs_orchard",  paymentMethod: "NETS",             affiliateId: "",       identityNo: "S9812345A" },
            { spNumber: "B02B504020025", orderNumber: "0026402025", orderDate: "22-Apr-26 07:30", type: "delivery",   customerName: "Chan Test",      contactNo: "+6521232323", email: "chan.test@market.digital",    deliveryDate: "22-Apr-26 15:26", deliveryOption: "3pm – 4pm",    orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Open",      processedBy: "tangs_content_cdm", tags: "",          store: "tangs_orchard",  paymentMethod: "NETS",             affiliateId: "",       identityNo: "S9812346B" },
            { spNumber: "B02B504020028", orderNumber: "0026402028", orderDate: "22-Apr-26 08:15", type: "collection", customerName: "Lee Wei Ming",   contactNo: "+6598765432", email: "weiming.lee@gmail.com",       deliveryDate: "-",              deliveryOption: "Collection",   orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Pending",   processedBy: "tangs_content_cdm", tags: "",          store: "tangs_vivocity", paymentMethod: "Credit Card",      affiliateId: "",       identityNo: "S8923456C" },
            { spNumber: "B02B504020031", orderNumber: "0026402031", orderDate: "22-Apr-26 09:00", type: "delivery",   customerName: "Priya Nair",     contactNo: "+6587654321", email: "priya.nair@outlook.com",      deliveryDate: "23-Apr-26 10:00", deliveryOption: "10am – 12pm",  orderStatus: "processing",  fulfilledBy: "Processing",  shippingStatus: "Pending",   processedBy: "tangs_content_cdm", tags: "",          store: "tangs_online",   paymentMethod: "PayNow",           affiliateId: "AFF001", identityNo: "F9234567K" },
            { spNumber: "B02B504020034", orderNumber: "0026402034", orderDate: "22-Apr-26 09:30", type: "express",    customerName: "Ahmad Razif",    contactNo: "+6591234567", email: "ahmad.r@yahoo.com",           deliveryDate: "22-Apr-26 14:00", deliveryOption: "Express 2hr",  orderStatus: "fulfilled",   fulfilledBy: "Fulfilled",   shippingStatus: "Shipped",   processedBy: "tangs_ops_cdm",     tags: "vip",       store: "tangs_orchard",  paymentMethod: "GrabPay",          affiliateId: "",       identityNo: "S7823456D" },
            { spNumber: "B02B504020037", orderNumber: "0026402037", orderDate: "22-Apr-26 10:15", type: "delivery",   customerName: "Jane Koh",       contactNo: "+6512345678", email: "jane.koh@singtel.com",        deliveryDate: "24-Apr-26 14:00", deliveryOption: "2pm – 4pm",    orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Open",      processedBy: "tangs_content_cdm", tags: "",          store: "tangs_vivocity", paymentMethod: "NETS",             affiliateId: "AFF002", identityNo: "S9034567E" },
            { spNumber: "B02B504020040", orderNumber: "0026402040", orderDate: "22-Apr-26 10:45", type: "delivery",   customerName: "David Tan",      contactNo: "+6565432109", email: "david.tan@company.sg",        deliveryDate: "25-Apr-26 10:00", deliveryOption: "10am – 12pm",  orderStatus: "processing",  fulfilledBy: "Processing",  shippingStatus: "Pending",   processedBy: "tangs_ops_cdm",     tags: "corporate", store: "tangs_online",   paymentMethod: "Corporate Account",affiliateId: "",       identityNo: "S8512345F" },
            { spNumber: "B02B504020043", orderNumber: "0026402043", orderDate: "22-Apr-26 11:00", type: "collection", customerName: "Sarah Lim",      contactNo: "+6578901234", email: "sarah.lim@gmail.com",         deliveryDate: "-",              deliveryOption: "Collection",   orderStatus: "fulfilled",   fulfilledBy: "Fulfilled",   shippingStatus: "Delivered", processedBy: "tangs_content_cdm", tags: "",          store: "tangs_orchard",  paymentMethod: "Credit Card",      affiliateId: "",       identityNo: "S9212345G" },
            { spNumber: "B02B504020046", orderNumber: "0026402046", orderDate: "22-Apr-26 11:30", type: "delivery",   customerName: "Wong Ah Kow",    contactNo: "+6545678901", email: "wak@hotmail.com",             deliveryDate: "26-Apr-26 14:00", deliveryOption: "2pm – 4pm",    orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Open",      processedBy: "tangs_content_cdm", tags: "",          store: "tangs_app",      paymentMethod: "Apple Pay",        affiliateId: "AFF001", identityNo: "S7634567H" },
            { spNumber: "B02B504020049", orderNumber: "0026402049", orderDate: "22-Apr-26 12:00", type: "delivery",   customerName: "Michelle Ng",    contactNo: "+6556789012", email: "michelle.ng@icloud.com",      deliveryDate: "26-Apr-26 18:00", deliveryOption: "6pm – 8pm",    orderStatus: "processing",  fulfilledBy: "Processing",  shippingStatus: "Pending",   processedBy: "tangs_ops_cdm",     tags: "gift",      store: "tangs_online",   paymentMethod: "PayNow",           affiliateId: "",       identityNo: "S9456789J" },
            { spNumber: "B02B504020052", orderNumber: "0026402052", orderDate: "22-Apr-26 12:30", type: "express",    customerName: "Ravi Kumar",     contactNo: "+6534567890", email: "ravi.k@techcorp.com",         deliveryDate: "22-Apr-26 15:00", deliveryOption: "Express 2hr",  orderStatus: "fulfilled",   fulfilledBy: "Fulfilled",   shippingStatus: "Shipped",   processedBy: "tangs_content_cdm", tags: "vip",       store: "tangs_orchard",  paymentMethod: "GrabPay",          affiliateId: "AFF003", identityNo: "G8823456K" },
            { spNumber: "B02B504020055", orderNumber: "0026402055", orderDate: "22-Apr-26 13:15", type: "delivery",   customerName: "Tan Mei Lin",    contactNo: "+6523456789", email: "meilin.tan@gmail.com",        deliveryDate: "27-Apr-26 10:00", deliveryOption: "10am – 12pm",  orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Open",      processedBy: "tangs_content_cdm", tags: "",          store: "tangs_vivocity", paymentMethod: "NETS",             affiliateId: "",       identityNo: "S8723456L" },
            { spNumber: "B02B504020058", orderNumber: "0026402058", orderDate: "22-Apr-26 13:45", type: "delivery",   customerName: "Kevin Chia",     contactNo: "+6512398765", email: "kevin.chia@outlook.sg",       deliveryDate: "27-Apr-26 14:00", deliveryOption: "2pm – 4pm",    orderStatus: "processing",  fulfilledBy: "Processing",  shippingStatus: "Pending",   processedBy: "tangs_ops_cdm",     tags: "member",    store: "tangs_orchard",  paymentMethod: "Credit Card",      affiliateId: "AFF002", identityNo: "S9623456M" },
            { spNumber: "B02B504020061", orderNumber: "0026402061", orderDate: "22-Apr-26 14:00", type: "collection", customerName: "Amy Goh",        contactNo: "+6567891234", email: "amy.goh@yahoo.com.sg",        deliveryDate: "-",              deliveryOption: "Collection",   orderStatus: "fulfilled",   fulfilledBy: "Fulfilled",   shippingStatus: "Delivered", processedBy: "tangs_content_cdm", tags: "",          store: "tangs_app",      paymentMethod: "Apple Pay",        affiliateId: "",       identityNo: "S8812345N" },
            { spNumber: "B02B504020064", orderNumber: "0026402064", orderDate: "22-Apr-26 14:30", type: "delivery",   customerName: "Lim Boon Keng",  contactNo: "+6578123456", email: "boonkeng.lim@gmail.com",      deliveryDate: "28-Apr-26 10:00", deliveryOption: "10am – 12pm",  orderStatus: "open",        fulfilledBy: "Open",        shippingStatus: "Open",      processedBy: "tangs_content_cdm", tags: "",          store: "tangs_online",   paymentMethod: "PayNow",           affiliateId: "AFF001", identityNo: "S7712345P" },
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
                if (f.doNumber       && !ci(o.spNumber, f.doNumber))           return false;
                if (f.orderNumber    && !ci(o.orderNumber, f.orderNumber))     return false;
                if (f.orderType      && o.type !== f.orderType)                return false;
                if (f.deliveryOption && !ci(o.deliveryOption, f.deliveryOption)) return false;
                if (f.shippingStatus && o.shippingStatus !== f.shippingStatus) return false;
                if (f.customerName   && !ci(o.customerName, f.customerName))   return false;
                if (f.contactNo      && o.contactNo.indexOf(f.contactNo) === -1) return false;
                if (f.email          && !ci(o.email, f.email))                 return false;
                if (f.processedBy    && !ci(o.processedBy, f.processedBy))     return false;
                if (f.paymentMethod  && o.paymentMethod !== f.paymentMethod)   return false;
                if (f.storeName      && !ci(o.store, f.storeName))             return false;
                if (f.affiliateId    && !ci(o.affiliateId, f.affiliateId))     return false;
                if (f.tags           && !ci(o.tags, f.tags))                   return false;
                return true;
            });
            vm.currentPage = 1;
            updatePaging();
        };

        /* ── View state (list / detail) ───────────────────────── */
        vm.view   = "list";
        vm.detail = null;

        vm.sections = { orderItems: true, remarks: true };
        vm.toggleSection = function (key) { vm.sections[key] = !vm.sections[key]; };

        /** S$ column: prefer server display string when non-empty; else format number (Angular number filter is blank for null). */
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
            vm.detail.doNumber = order.spNumber || order.doNumber || "";
            vm.detail.items = [];

            /* Rich detail shape (legacy OrderDetailProcessing-aligned) — replace with Order.Api */
            /* Set receiptMode to 'collection' | 'delivery' | 'gift' + printLogoUrl / barcodeSrc to show print header */
            vm.detail.printLogoUrl = "";
            vm.detail.barcodeSrc = "";
            vm.detail.deliveryOrderCount = 1;
            vm.detail.doDetailUrl = "";

            vm.detail.orderDateDisplay = order.orderDate;
            vm.detail.orderStatus = order.orderStatus || "open";
            vm.detail.orderStatusLabel = order.fulfilledBy || "Open";

            vm.detail.membershipCard = "P0000017303";
            vm.detail.membershipType = "Tangs-Member";
            vm.detail.membershipTier = "Preferred";
            vm.detail.billingAddress = "-\n-\nSG 238864";
            vm.detail.cardNo = "P0000017303";

            vm.detail.orderPromotionCode = "N.A.";
            vm.detail.orderPromotionCodeAmount = 0;
            vm.detail.rebatesCode = "REBATES";
            vm.detail.rebatesAmount = 3;
            vm.detail.rebatesItem = "3.00";
            vm.detail.invoiceUrl = "https://tangs-uat.ascentisecommerce.com/invoice/d3oyekRDQUNNVndKdG1Ka1dmV3pmTlZmWDJnOTNNTGRTNWZDRjRPSW1Mdz0=";
            vm.detail.gst = 2.48;
            vm.detail.baseQ = 17.43;
            vm.detail.baseF = 0;
            vm.detail.baseN = 10.09;
            vm.detail.baseNP = 0;
            vm.detail.openingBal = 139200;
            vm.detail.rebateEarned = 100;

            vm.detail.totalOrderAmount = 19;
            vm.detail.extraFees = 0;
            vm.detail.totalHandlingFee = 0;
            vm.detail.totalDeliveryFee = 14;
            vm.detail.totalOrderDiscount = 3;
            vm.detail.tPointsEarned = 1;
            vm.detail.tPointsRedeem = 0;
            vm.detail.giftCardUsed = "N.A.";
            vm.detail.giftCardAmount = 0;
            vm.detail.totalAmountPayable = 30;
            vm.detail.paymentAmount = 30;
            vm.detail.paymentMethod = "Adyen Visa";

            vm.detail.deliveryOption = "7 DAYS | SW";
            vm.detail.deliveryDateDisplay = "09 Apr 2026 23:59:59";
            vm.detail.timeslot = "N.A.";
            vm.detail.deliveryAddress = vm.detail.billingAddress;

            vm.detail.deliveryItemsAmount = 16;
            vm.detail.deliveryExtraFees = 0;
            vm.detail.handlingFee = 0;
            vm.detail.deliveryFee = 14;
            vm.detail.deliveryFeeDiscount = 0;

            vm.detail.giftWrap = "No";
            vm.detail.giftExchangeReceipt = "No";
            vm.detail.giftMessage = "N.A.";
            vm.detail.deliveryRemarks = "N.A.";
            vm.detail.shippingTagInput = "";

            vm.detail.trackingNumber = "N.A.";
            vm.detail.lastMileStatus = "N.A.";
            vm.detail.actualDeliveryDate = "N.A.";
            vm.detail.actualDeliveryFee = 14;
            vm.detail.fulfillment = "TANGS";
            vm.detail.standardDeliveryNote = "*Your order will be delivered within 7 days.";

            vm.detail.orderItems = [
                {
                    brand: "AIMER MEN",
                    imageUrl: "https://tangs-uat-cdn.ascentismedia.com/SharedImages/ProductImagesES/c686214e-9b17-4743-ac6b-d351b987bbc9/1/c686214e-9b17-4743-ac6b-d351b987bbc9_190x190.jpg",
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
                    text: "Change Order Details Info<br>• Changed RebateEarned From '0.0000' to '1.0'<br>• Changed OtherInfo :<br>- Field 'GST' with value '2.48' is added<br> - Field 'BASEQ' with value '17.43' is added<br> - Field 'BASEF' with value '0' is added<br> - Field 'BASEN' with value '10.09' is added<br> - Field 'BASENP' with value '0' is added<br> - Field 'OpeningBal' with value '139200' is added<br> - Field 'RebateEarned' with value '100' is added<br> ",
                    addedBy: "tangs_system_user_uat",
                    addedOn: "02/04/2026 5:57:27 PM",
                },
                {
                    text: "Change Delivery Order Info DO2604020014<br>• Changed ActualDeliveryFee From '' to '14.0'<br>",
                    addedBy: "tangs_system_user_uat",
                    addedOn: "02/04/2026 5:57:20 PM",
                },
                {
                    text: "Change Order Details Info<br>• Changed RebateEarned From '' to '0.0'<br>• Changed OtherInfo :<br>- Field 'InvoiceUrl' with value 'https://tangs-uat.ascentisecommerce.com/invoice/d3oyekRDQUNNVndKdG1Ka1dmV3pmTlZmWDJnOTNNTGRTNWZDRjRPSW1Mdz0=' is added<br> ",
                    addedBy: "tangs_system_user_uat",
                    addedOn: "02/04/2026 5:57:20 PM",
                },
            ];

            vm.sections = { orderItems: true, remarks: true };
            vm.view = "detail";
            $window.scrollTo(0, 0);
        };

        vm.backToList = function () {
            vm.view   = "list";
            vm.detail = null;
        };

        vm.saveFilters = function () { /* persist to user prefs via API */ };
        vm.export      = function () { /* call export endpoint */ };

        updatePaging();
    }

    DcmsOrderProcessingController.$inject = ["$scope", "$sce", "$window"];
    angular.module("umbraco").controller("DcmsOrderProcessingController", DcmsOrderProcessingController);
})();
