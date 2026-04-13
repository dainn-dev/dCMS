/**
 * DAI-298 — Shared Catalog JWT RBAC helpers for Umbraco AngularJS (ForwardIdentity).
 * Mirrors Catalog.Api policies: catalog:write = StoreManager+ (not StoreStaff).
 */
(function () {
    "use strict";

    var forwardIdentityUrl = "/umbraco/backoffice/DcmsCatalog/CatalogBackofficeProxy/ForwardIdentity";

    function parseEnvelope(res) {
        var d = res.data;
        if (d && d.error) return { err: d.error.message || d.error.code || "error", data: null };
        return { err: null, data: d ? d.data : null };
    }

    function computeSnapshot(data) {
        var roles = (data && data.roles) || [];
        var norm = roles.map(function (r) {
            return (r || "").trim();
        });
        function has(role) {
            var t = (role || "").toLowerCase();
            for (var i = 0; i < norm.length; i++) {
                if ((norm[i] || "").toLowerCase() === t) return true;
            }
            return false;
        }
        var canRunCatalogApprovalActions = !!(data && data.canRunCatalogApprovalActions);
        var canCatalogWrite =
            data && typeof data.canCatalogWrite === "boolean"
                ? data.canCatalogWrite
                : has("SuperAdmin") || has("ChainAdmin") || has("BrandManager") || has("StoreManager");
        return {
            roles: roles,
            canRunCatalogApprovalActions: canRunCatalogApprovalActions,
            canCatalogWrite: canCatalogWrite,
            isStoreStaff: has("StoreStaff") && !canCatalogWrite,
            hasRole: function (role) {
                return has(role);
            },
        };
    }

    angular.module("umbraco").factory("dcmsCatalogRbac", [
        "$http",
        "$q",
        function ($http, $q) {
            var cache = null;
            var inflight = null;

            return {
                /** Clears cache (e.g. after Umbraco user switch). */
                reset: function () {
                    cache = null;
                    inflight = null;
                },
                peek: function () {
                    return cache;
                },
                /** Cached GET ForwardIdentity; safe on failure (read-only fallback). */
                ensureLoaded: function () {
                    if (cache) return $q.resolve(cache);
                    if (inflight) return inflight;
                    inflight = $http
                        .get(forwardIdentityUrl)
                        .then(function (res) {
                            var p = parseEnvelope(res);
                            if (p.err) {
                                cache = computeSnapshot({ roles: [], canRunCatalogApprovalActions: false });
                                cache._loadError = p.err;
                            } else {
                                cache = computeSnapshot(p.data || {});
                            }
                            cache._loaded = true;
                            inflight = null;
                            return cache;
                        })
                        .catch(function () {
                            cache = computeSnapshot({ roles: [], canRunCatalogApprovalActions: false });
                            cache._loaded = true;
                            cache._loadError = "network";
                            inflight = null;
                            return cache;
                        });
                    return inflight;
                },
            };
        },
    ]);
})();
