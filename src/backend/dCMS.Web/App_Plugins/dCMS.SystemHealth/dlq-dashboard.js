/**
 * US-F4 / DAI-363 — Order DLQ viewer (poll30s). Calls Umbraco BFF → Order.Api admin routes.
 */
(function () {
    "use strict";

    var api = "/umbraco/backoffice/DcmsSystemHealth";

    function parseEnvelope(res) {
        var d = res.data;
        if (typeof d === "string") {
            try { d = JSON.parse(d); } catch (e) { return { err: "invalid json", rows: [] }; }
        }
        if (d && d.error) {
            return { err: d.error.message || d.error.code || "error", rows: [] };
        }
        return { err: null, rows: (d && d.data) ? d.data : [] };
    }

    angular.module("umbraco").controller("dcmsSystemHealthDlq", [
        "$http",
        "$interval",
        function ($http, $interval) {
            var vm = this;
            vm.rows = [];
            vm.busy = false;
            vm.error = null;
            vm.eventType = "";
            vm.includeDiscarded = false;

            vm.load = function () {
                vm.busy = true;
                vm.error = null;
                var url = api + "/OrderDlqList?includeDiscarded=" + (vm.includeDiscarded ? "true" : "false");
                if (vm.eventType) {
                    url += "&eventType=" + encodeURIComponent(vm.eventType);
                }
                $http.get(url).then(function (res) {
                    var p = parseEnvelope(res);
                    vm.error = p.err;
                    vm.rows = p.rows || [];
                }, function (err) {
                    vm.error = (err && err.data) ? JSON.stringify(err.data) : "request failed";
                    vm.rows = [];
                }).finally(function () {
                    vm.busy = false;
                });
            };

            vm.retry = function (r) {
                if (!confirm("Retry DLQ id " + r.id + "?")) return;
                $http.post(api + "/OrderDlqRetry", { id: r.id }).then(function (res) {
                    var p = parseEnvelope(res);
                    if (p.err) { alert(p.err); return; }
                    vm.load();
                }, function (err) {
                    alert(err.status + " retry failed");
                });
            };

            vm.discard = function (r) {
                var reason = prompt("Discard reason", "discarded_from_backoffice");
                if (reason === null) return;
                $http.post(api + "/OrderDlqDiscard", { id: r.id, reason: reason }).then(function (res) {
                    var p = parseEnvelope(res);
                    if (p.err) { alert(p.err); return; }
                    vm.load();
                }, function (err) {
                    alert(err.status + " discard failed");
                });
            };

            vm.load();
            $interval(vm.load, 30000);
        }
    ]);
})();
