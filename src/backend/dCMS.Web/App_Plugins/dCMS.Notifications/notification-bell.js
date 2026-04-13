/**
 * DAI-297 — Backoffice notification bell: poll unread count (30s), dropdown list, PATCH read-all on open.
 * Mounts into Umbraco header when DOM is ready (AngularJS + legacy backoffice).
 */
(function () {
    "use strict";

    var bellApiBase = "/umbraco/backoffice/DcmsCatalog/NotificationBellProxy";

    function parseEnvelope(res) {
        var d = res.data;
        if (d && d.error) {
            return { err: d.error.message || d.error.code || "error", data: null };
        }
        return { err: null, data: d ? d.data : null };
    }

    function readTenantStore(vm) {
        try {
            vm.tenantId = localStorage.getItem("dcmsBell_tenantId") || "t1";
            vm.storeId = localStorage.getItem("dcmsBell_storeId") || "s1";
        } catch (x) {
            vm.tenantId = "t1";
            vm.storeId = "s1";
        }
    }

    angular.module("umbraco").directive("dcmsNotificationBell", [
        "$http",
        "$interval",
        "dcmsCatalogRbac",
        function ($http, $interval, dcmsCatalogRbac) {
            return {
                restrict: "E",
                replace: true,
                templateUrl: "/App_Plugins/dCMS.Notifications/notification-bell.partial.html",
                link: function (scope) {
                    scope.vm = {
                        count: 0,
                        lastCount: null,
                        items: [],
                        open: false,
                        busy: false,
                        error: null,
                        tenantId: "t1",
                        storeId: "s1",
                        /** DAI-298: PATCH read-all only when catalog:write (not StoreStaff). */
                        canCatalogWrite: true,
                    };

                    dcmsCatalogRbac.ensureLoaded().then(function (ctx) {
                        scope.vm.canCatalogWrite = !!ctx.canCatalogWrite;
                    });

                    scope.vm.iconClass = function (type) {
                        var t = ((type || "") + "").toLowerCase();
                        if (t.indexOf("approval") >= 0 || t.indexOf("submit") >= 0) return "glyphicon-upload text-info";
                        if (t.indexOf("reject") >= 0) return "glyphicon-remove text-danger";
                        if (t.indexOf("request") >= 0) return "glyphicon-warning-sign text-warning";
                        if (t.indexOf("product") >= 0) return "glyphicon-shopping-cart text-muted";
                        return "glyphicon-bell text-muted";
                    };

                    scope.vm.loadList = function () {
                        readTenantStore(scope.vm);
                        return $http
                            .get(bellApiBase + "/List", {
                                params: { tenantId: scope.vm.tenantId, storeId: scope.vm.storeId, limit: 20 },
                            })
                            .then(function (res) {
                                var p = parseEnvelope(res);
                                if (p.err) {
                                    scope.vm.error = p.err;
                                    return;
                                }
                                scope.vm.items = (p.data && p.data.items) || [];
                            });
                    };

                    scope.vm.fetchUnread = function () {
                        readTenantStore(scope.vm);
                        scope.vm.error = null;
                        return $http
                            .get(bellApiBase + "/UnreadCount", {
                                params: { tenantId: scope.vm.tenantId, storeId: scope.vm.storeId },
                            })
                            .then(function (res) {
                                var p = parseEnvelope(res);
                                if (p.err) {
                                    scope.vm.error = p.err;
                                    return;
                                }
                                var c = (p.data && typeof p.data.count === "number" ? p.data.count : 0) || 0;
                                if (scope.vm.lastCount !== null && c !== scope.vm.lastCount) {
                                    scope.vm.loadList();
                                }
                                scope.vm.lastCount = c;
                                scope.vm.count = c;
                            });
                    };

                    scope.vm.toggleOpen = function () {
                        scope.vm.open = !scope.vm.open;
                        if (scope.vm.open) {
                            scope.vm.busy = true;
                            readTenantStore(scope.vm);
                            var afterOpen = function () {
                                return scope.vm.fetchUnread().then(function () {
                                    return scope.vm.loadList();
                                });
                            };
                            if (scope.vm.canCatalogWrite) {
                                $http
                                    .patch(bellApiBase + "/ReadAll", undefined, {
                                        params: { tenantId: scope.vm.tenantId, storeId: scope.vm.storeId },
                                    })
                                    .then(afterOpen)
                                    .finally(function () {
                                        scope.vm.busy = false;
                                    });
                            } else {
                                afterOpen().finally(function () {
                                    scope.vm.busy = false;
                                });
                            }
                        }
                    };

                    scope.vm.onItemClick = function (item) {
                        var t = ((item.type || "") + "").toLowerCase();
                        var id = (item.entityId || "").trim();
                        if (id && (t.indexOf("product") >= 0 || !t)) {
                            try {
                                localStorage.setItem("dcmsCatalogWizard_navigateProductId", id);
                            } catch (x) {}
                            window.location.href = "/umbraco/#/dCMSCatalog/dCMSProductWizard";
                        }
                        scope.vm.open = false;
                    };

                    var stopPoll = $interval(function () {
                        scope.vm.fetchUnread();
                    }, 30000);

                    scope.vm.fetchUnread().then(function () {
                        return scope.vm.loadList();
                    });

                    scope.$on("$destroy", function () {
                        if (stopPoll) $interval.cancel(stopPoll);
                    });

                    scope.$on("dcms:refreshCatalogData", function () {
                        scope.vm.fetchUnread().catch(function () {});
                        if (scope.vm.open) scope.vm.loadList().catch(function () {});
                    });

                    var docEl = angular.element(document);
                    var docClick = function (e) {
                        if (!scope.vm.open) return;
                        var el = e.target;
                        if (!el || typeof el.closest !== "function") return;
                        if (el.closest(".dcms-notif-bell")) return;
                        scope.$applyAsync(function () {
                            scope.vm.open = false;
                        });
                    };
                    docEl.on("click", docClick);
                    scope.$on("$destroy", function () {
                        docEl.off("click", docClick);
                    });
                },
            };
        },
    ]);

    angular.module("umbraco").run([
        "$rootScope",
        "$compile",
        "$timeout",
        function ($rootScope, $compile, $timeout) {
            function mount() {
                if (document.getElementById("dcms-notification-bell-root")) return;
                var nav =
                    document.querySelector(".umb-app-header__right") ||
                    document.querySelector(".umb-app-header__navigation") ||
                    document.querySelector(".umb-app-header");
                if (!nav) return;
                var holder = angular.element(
                    '<span id="dcms-notification-bell-root" style="display:inline-block;vertical-align:middle;margin-right:8px;"></span>'
                );
                angular.element(nav).prepend(holder);
                holder.append($compile("<dcms-notification-bell></dcms-notification-bell>")($rootScope.$new(true)));
            }
            $timeout(mount, 2000);
            $timeout(mount, 6000);
        },
    ]);
})();
