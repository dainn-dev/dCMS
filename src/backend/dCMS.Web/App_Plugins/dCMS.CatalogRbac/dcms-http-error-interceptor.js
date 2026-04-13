/**
 * DAI-299 — $http responseError for dCMS BFF calls (Umbraco AngularJS).
 * Targets URLs under /umbraco/backoffice/DcmsCatalog (PluginController area; no /api/ segment).
 */
(function () {
    "use strict";

    function isDcmsBackofficeRequest(url) {
        return typeof url === "string" && url.indexOf("/umbraco/backoffice/DcmsCatalog") === 0;
    }

    angular.module("umbraco").factory("dcmsHttpErrorInterceptor", [
        "$q",
        "$injector",
        function ($q, $injector) {
            return {
                responseError: function (rejection) {
                    var cfg = rejection.config || {};
                    var url = cfg.url || "";
                    if (!isDcmsBackofficeRequest(url)) return $q.reject(rejection);

                    var status = rejection.status || 0;
                    var notifications;
                    try {
                        notifications = $injector.get("notificationsService");
                    } catch (e) {
                        return $q.reject(rejection);
                    }

                    if (status === 401) {
                        try {
                            sessionStorage.setItem("dcmsReturnAfterLogin", window.location.hash || window.location.href);
                        } catch (x) {}
                        var ret =
                            window.location.pathname +
                            window.location.search +
                            (window.location.hash || "");
                        window.location.assign("/umbraco/login?ReturnUrl=" + encodeURIComponent(ret));
                        return $q.reject(rejection);
                    }

                    if (status === 403) {
                        notifications.error("Access denied", "You do not have permission for this action.");
                        return $q.reject(rejection);
                    }

                    if (status === 409) {
                        notifications.warning("Concurrency", "Data may have changed. Refreshing open dCMS views.");
                        try {
                            $injector.get("$rootScope").$broadcast("dcms:refreshCatalogData");
                        } catch (x) {}
                        return $q.reject(rejection);
                    }

                    if (status === 422) {
                        var d = rejection.data || {};
                        var msg = (d.error && (d.error.message || d.error.code)) || rejection.statusText || "Request failed";
                        var meta = d.meta;
                        var detail = "";
                        if (meta != null) {
                            try {
                                detail = typeof meta === "string" ? meta : JSON.stringify(meta);
                            } catch (j) {
                                detail = "";
                            }
                        }
                        notifications.error("Request rejected", detail ? msg + " — " + detail : msg);
                        return $q.reject(rejection);
                    }

                    if (status >= 500 && status < 600) {
                        notifications.error("Server error", "HTTP " + status + ". Please retry in a moment.");
                        return $q.reject(rejection);
                    }

                    return $q.reject(rejection);
                },
            };
        },
    ]);

    angular.module("umbraco").config([
        "$httpProvider",
        function ($httpProvider) {
            $httpProvider.interceptors.push("dcmsHttpErrorInterceptor");
        },
    ]);
})();
