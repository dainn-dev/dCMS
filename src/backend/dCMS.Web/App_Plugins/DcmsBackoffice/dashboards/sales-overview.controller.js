(function () {
    "use strict";

    /**
     * Sales area chart via ApexCharts (vanilla). Umbraco backoffice = AngularJS;
     * ng-apexcharts is for Angular 2+ — we call new ApexCharts(el, opts) like the core docs.
     * Style reference: https://apexcharts.com/angular-chart-demos/area-charts/stacked-area/
     */
    function DcmsSalesOverviewController($scope, $timeout) {
        var vm = this;
        vm.period = "day";

        /* ── Widget data (replace with real API calls when available) ── */
        vm.widgets = {
            campaigns: [],   // empty → shows "No Data Available"
            newOrders: [
                { orderNumber: "S$2504020012", amount: "S$30.00" },
                { orderNumber: "S$2504020011", amount: "S$20.00" },
                { orderNumber: "S$2504020010", amount: "S$94.00" },
                { orderNumber: "S$2504020009", amount: "S$20.00" },
                { orderNumber: "S$2504020008", amount: "S$44.00" },
            ],
            topProducts: [
                { name: "Home Department Hust...", amount: "S$6,687.00" },
                { name: "Jellycat Bashful Thr...", amount: "S$2,971.38" },
                { name: "TANGS e-Gift Card",       amount: "S$1,420.00" },
                { name: "Jo",                      amount: "S$958.20"   },
                { name: "HAPPY SOCKS X BEATLE...", amount: "S$636.00"   },
            ],
            topCategories: [
                { name: "STAFF-DISCOUNTS", amount: "S$9,258.20" },
                { name: "HOME",            amount: "S$7,840.20" },
                { name: "BIG DISCOUNT",    amount: "S$5,968.10" },
                { name: "Bed & Bath",      amount: "S$5,777.10" },
                { name: "KIDS",            amount: "S$3,304.68" },
            ],
            topBrands: [
                { name: "River Home",      amount: "S$6,687.00" },
                { name: "Jellycat",        amount: "S$2,971.38" },
                { name: "TANGS eGift Card",amount: "S$1,420.00" },
                { name: "Dyson",           amount: "S$958.20"   },
                { name: "Tom Ford",        amount: "S$928.00"   },
            ],
            salesByMemberType: [
                { type: "Tangs-Member", txCount: 53, amount: "S$14,491.20" },
                { type: "NonMember",    txCount: 12, amount: "S$2,714.00"  },
            ],
            salesByMemberTier: [
                { tier: "Classic",    txCount: 16, amount: "S$10,250.10" },
                { tier: "Preferred",  txCount: 37, amount: "S$3,303.60"  },
                { tier: "NonMember",  txCount: 12, amount: "S$2,714.00"  },
                { tier: "Insider",    txCount: 10, amount: "S$935.50"    },
            ],
            salesByAppSource: [
                { source: "DESKTOP",   txCount: 73, amount: "S$17,118.20" },
                { source: "MOBILEAPP", txCount: 2,  amount: "S$89.00"     },
            ],
        };

        vm.datasets = {
            day: {
                labels: ["Feb 24", "Feb 28", "Mar 4", "Mar 8", "Mar 12", "Mar 16", "Mar 20", "Mar 24", "Mar 28", "Apr 3"],
                values: [1200, 1650, 3100, 2400, 6986, 4000, 2050, 4700, 3100, 6420],
            },
            month: {
                labels: ["Nov", "Dec", "Jan", "Feb", "Mar", "Apr"],
                values: [42000, 38500, 51200, 47800, 55600, 44800],
            },
        };

        var chart = null;
        var rootSelector = "#dcms-sales-dashboard-root .dcms-sales-apex-inner";

        function formatMoney(n) {
            return "S$" + Math.round(n).toLocaleString("en-SG");
        }

        function hostHeight() {
            var outer = document.querySelector("#dcms-sales-dashboard-root .dcms-sales-apex-host");
            var inner = document.querySelector(rootSelector);
            var el = outer || inner;
            if (!el) return 300;
            var h = el.clientHeight;
            return h > 80 ? h : 300;
        }

        function buildOptions() {
            var ds = vm.datasets[vm.period];
            var day = vm.period === "day";

            return {
                chart: {
                    type: "area",
                    height: hostHeight(),
                    width: "100%",
                    toolbar: { show: false },
                    zoom: { enabled: false },
                    animations: { enabled: true, speed: 350 },
                    fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
                    foreColor: "#64748b",
                },
                series: [{ name: "Sales", data: ds.values.slice() }],
                xaxis: {
                    categories: ds.labels.slice(),
                    labels: {
                        style: { fontSize: "11px", colors: "#64748b" },
                        rotate: day ? -40 : 0,
                        rotateAlways: day,
                        hideOverlappingLabels: true,
                    },
                    axisBorder: { show: true, color: "#e2e8f0" },
                    axisTicks: { show: true, color: "#e2e8f0" },
                    title: {
                        text: day ? "Days (sample)" : "Months (sample)",
                        style: { fontSize: "11px", fontWeight: 500, color: "#64748b" },
                    },
                },
                yaxis: {
                    title: {
                        text: "Sales (S$)",
                        style: { fontSize: "11px", fontWeight: 500, color: "#64748b" },
                    },
                    labels: {
                        style: { fontSize: "11px", colors: "#94a3b8" },
                        formatter: function (val) {
                            return formatMoney(val);
                        },
                    },
                },
                stroke: {
                    curve: "smooth",
                    width: 2.5,
                    colors: ["#d42054"],
                },
                fill: {
                    type: "gradient",
                    gradient: {
                        shadeIntensity: 1,
                        opacityFrom: 0.5,
                        opacityTo: 0.04,
                        stops: [0, 88, 100],
                    },
                },
                colors: ["#d42054"],
                dataLabels: { enabled: false },
                markers: {
                    size: 0,
                    hover: { size: 5 },
                },
                grid: {
                    borderColor: "#f1f5f9",
                    strokeDashArray: 0,
                    padding: { left: 4, right: 12, top: 8, bottom: 4 },
                },
                legend: { show: false },
                tooltip: {
                    theme: "light",
                    y: {
                        formatter: function (val) {
                            return formatMoney(val);
                        },
                    },
                },
            };
        }

        function mountChart() {
            if (typeof ApexCharts === "undefined") {
                return;
            }
            var el = document.querySelector(rootSelector);
            if (!el) return;

            if (chart) {
                chart.destroy();
                chart = null;
            }

            chart = new ApexCharts(el, buildOptions());
            chart.render();
        }

        function refreshChart() {
            if (typeof ApexCharts === "undefined") {
                return;
            }
            var ds = vm.datasets[vm.period];
            var day = vm.period === "day";

            if (!chart) {
                mountChart();
                return;
            }

            chart.updateOptions(
                {
                    chart: { height: hostHeight() },
                    series: [{ name: "Sales", data: ds.values.slice() }],
                    xaxis: {
                        categories: ds.labels.slice(),
                        labels: {
                            rotate: day ? -40 : 0,
                            rotateAlways: day,
                            hideOverlappingLabels: true,
                        },
                        title: {
                            text: day ? "Days (sample)" : "Months (sample)",
                        },
                    },
                },
                false,
                true
            );
        }

        vm.setPeriod = function (p) {
            vm.period = p;
            refreshChart();
        };

        var win = angular.element(window);
        var resizeScheduled = null;
        function onResize() {
            if (resizeScheduled) clearTimeout(resizeScheduled);
            resizeScheduled = setTimeout(function () {
                resizeScheduled = null;
                if (!chart) return;
                var h = hostHeight();
                chart.updateOptions({ chart: { height: h } }, false, false);
            }, 200);
        }
        win.on("resize.dcmsSalesApex", onResize);

        $scope.$on("$destroy", function () {
            win.off("resize.dcmsSalesApex", onResize);
            if (resizeScheduled) clearTimeout(resizeScheduled);
            if (chart) {
                chart.destroy();
                chart = null;
            }
        });

        $timeout(mountChart, 0);
    }

    DcmsSalesOverviewController.$inject = ["$scope", "$timeout"];
    angular.module("umbraco").controller("DcmsSalesOverviewController", DcmsSalesOverviewController);
})();
