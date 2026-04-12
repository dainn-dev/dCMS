/**
 * US-13 — 5-step Store Manager product wizard (AngularJS, Umbraco backoffice).
 * Uses BFF: SlugCheck (GET) + Forward (POST) → Catalog.Api.
 */
angular.module("umbraco").controller("Umbraco.dCMS.ProductWizardController", [
    "$http",
    "$timeout",
    "$scope",
    "$q",
    "notificationsService",
    "dcmsCatalogRbac",
    function ($http, $timeout, $scope, $q, notificationsService, dcmsCatalogRbac) {
        var vm = this;
        vm.title = "dCMS — Product wizard";
        vm.step = 1;
        vm.tenantId = "t1";
        vm.storeId = "s1";
        vm.categories = [];
        vm.categoryById = {};
        vm.expandedByCategoryId = {};
        vm.leafCategories = [];
        vm.selectedCategoryId = null;
        vm.nameTab = "vi";
        vm.nameVi = "";
        vm.nameEn = "";
        vm.descVi = "";
        vm.descEn = "";
        vm.slug = "";
        vm.slugManual = false;
        vm.slugState = null;
        vm.slugPending = false;
        vm.slugCache = {};
        vm.productId = null;
        vm.axesJson = "[]";
        vm.variantAxesDefinitions = [];
        vm.selectedValueIds = {};
        vm.skuPrefix = "sku";
        vm.generateResult = null;
        vm.publishOk = false;
        vm.error = null;
        vm.busy = false;
        /** Step 5 (DAI-286): windowed variant grid + bulk status/sort + save via PUT chain. */
        vm.variantsList = [];
        vm.variantsBaseline = {};
        vm.variantEdits = {};
        vm.selectedVariantIds = {};
        vm.variantRowHeight = 40;
        vm.variantGridViewportPx = 360;
        vm.variantGridBuffer = 8;
        vm.variantGridScrollTop = 0;
        vm.variantGridLayout = { start: 0, end: 0, offsetY: 0, innerH: 0 };
        vm.visibleVariantRows = [];
        vm.bulkStatusTarget = "active";
        vm.bulkSortStart = 0;
        vm.variantSaveProgress = null;
        /** Step 5 (DAI-287): GET product + review summary + publish/hide + optional detail URL. */
        vm.productDetail = null;
        /** DAI-295: GET …/approval-comments + 15s poll while pending_approval. */
        vm.approvalComments = [];
        vm.approvalCommentsPollTimer = null;
        /** DAI-296: roles from BFF ForwardIdentity + approve / request-changes / reject. */
        vm.catalogIdentity = null;
        /** DAI-299: shimmer while first variants GET for this product is in flight. */
        vm.variantsGridSkeleton = false;
        vm.approvalActionModal = null;
        vm.approvalActionComment = "";
        vm.step5Notice = null;
        /** US-14: Step 5 sub-views */
        vm.step5Tab = "variants";
        vm.variantFilterText = "";
        vm.variantSortKey = "sortOrder";
        vm.variantDisplayList = [];
        vm.bulkPriceInput = "";
        vm.priceUndoSnapshot = null;
        vm.priceUndoTimer = null;
        vm.variantRowFlash = {};
        vm.variantRowSaving = {};
        /** DAI-288: manual add variant + hash conflict highlight */
        vm.newManualVariant = { sku: "", combinationHash: "", combinationCanonical: "", basePriceAmount: 0, status: "active" };
        vm.variantHashConflictId = null;
        vm.variantHashConflictHash = null;
        vm.imagesList = [];
        vm.imageBusy = false;
        /** DAI-292: Inventory BFF + multi-warehouse stock grid */
        vm.inventoryWarehouses = [];
        vm.selectedStockWarehouseId = "";
        vm.stockByVariant = {};
        vm.stockBusy = false;
        vm.stockGridLoaded = false;
        vm.stockAutoTimer = null;
        vm.stockAdjustModal = null;
        vm.stockAdjustDelta = 0;
        vm.stockAdjustType = "adjustment";
        vm.stockAdjustNote = "";
        vm.bulkStockRows = [];
        vm.bulkStockParseError = null;
        vm.bulkStockLastResult = null;
        vm.storePublishMode = "direct";
        vm.storeCatalogSettingsBusy = false;
        /** DAI-280: last Catalog/Inventory forward failed offline — retry reuses Idempotency-Key. */
        vm.forwardRetry = null;
        vm.approvalReplyText = "";
        vm.productUrlTemplate = "";
        try {
            var pm = localStorage.getItem("dcmsCatalogWizard_publishMode");
            vm.storePublishMode = pm === "approval" ? "approval" : "direct";
            vm.productUrlTemplate = localStorage.getItem("dcmsCatalogWizard_productUrlTemplate") || "";
        } catch (x) {}
        var openNavProduct = false;
        try {
            var navPid = localStorage.getItem("dcmsCatalogWizard_navigateProductId");
            if (navPid) {
                localStorage.removeItem("dcmsCatalogWizard_navigateProductId");
                vm.productId = navPid;
                vm.step = 5;
                openNavProduct = true;
            }
        } catch (xNav) {}
        try {
            localStorage.setItem("dcmsBell_tenantId", vm.tenantId || "t1");
            localStorage.setItem("dcmsBell_storeId", vm.storeId || "s1");
        } catch (xBell) {}

        var slugTimer = null;
        var baseApi = "/umbraco/backoffice/api/DcmsCatalog/CatalogBackofficeProxy";
        var baseInventoryApi = "/umbraco/backoffice/api/DcmsCatalog/InventoryBackofficeProxy";

        function slugify(s) {
            if (!s) return "";
            try {
                s = s.normalize("NFD").replace(/[\u0300-\u036f]/g, "");
            } catch (x) {}
            return s
                .toLowerCase()
                .trim()
                .replace(/[^a-z0-9]+/g, "-")
                .replace(/^-+|-+$/g, "")
                .substring(0, 200);
        }

        function parseEnvelope(res) {
            var d = res.data;
            if (d && d.error) {
                return { err: d.error.message || d.error.code || "error", data: null, meta: d.meta || null };
            }
            return { err: null, data: d ? d.data : null, meta: d ? d.meta : null };
        }

        function bytesToHex(buffer) {
            var u8 = new Uint8Array(buffer);
            var h = "";
            for (var i = 0; i < u8.length; i++) {
                var x = u8[i].toString(16);
                h += x.length === 1 ? "0" + x : x;
            }
            return h;
        }

        vm.sha256HexOfArrayBuffer = function (buf) {
            if (!window.crypto || !crypto.subtle) return $q.reject(new Error("SHA-256 not available in this browser."));
            return crypto.subtle.digest("SHA-256", buf).then(bytesToHex);
        };

        vm.forwardBinaryPut = function (path, arrayBuffer, contentType) {
            vm.error = null;
            vm.busy = true;
            var bytes = new Uint8Array(arrayBuffer);
            var binary = "";
            var chunk = 0x8000;
            for (var i = 0; i < bytes.length; i += chunk) {
                binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
            }
            var b64 = btoa(binary);
            var headers = { "Content-Type": "application/json" };
            if (window.crypto && crypto.randomUUID) headers["Idempotency-Key"] = crypto.randomUUID();
            return $http
                .post(
                    baseApi + "/Forward",
                    {
                        method: "PUT",
                        path: path,
                        tenantId: vm.tenantId,
                        storeId: vm.storeId,
                        binaryBodyBase64: b64,
                        binaryContentType: contentType || "application/octet-stream",
                    },
                    { headers: headers }
                )
                .then(function (res) {
                    var p = parseEnvelope(res);
                    if (p.err) throw new Error(p.err);
                    return p.data;
                })
                .catch(function (e) {
                    var d = e.data || {};
                    if (d.error) throw new Error(d.error.message || d.error.code || "Request failed");
                    throw e;
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.minEditedVariantPrice = function () {
            var m = null;
            angular.forEach(vm.variantsList || [], function (v) {
                var e = vm.variantEdits[v.id];
                if (!e) return;
                var x = Number(e.basePriceAmount);
                if (Number.isNaN(x)) return;
                m = m === null ? x : Math.min(m, x);
            });
            return m === null ? 0 : m;
        };

        vm.recomputeVariantDisplayList = function () {
            var src = vm.variantsList || [];
            var f = (vm.variantFilterText || "").toLowerCase().trim();
            var out = [];
            angular.forEach(src, function (v) {
                var e = vm.variantEdits[v.id] || {};
                var sku = ((e.sku != null ? e.sku : v.sku) || "").toLowerCase();
                var h = (v.combinationHash || "").toLowerCase();
                var canon = ((v.combinationCanonical || "") + "").toLowerCase();
                if (!f || sku.indexOf(f) >= 0 || h.indexOf(f) >= 0 || canon.indexOf(f) >= 0) out.push(v);
            });
            var key = vm.variantSortKey || "sortOrder";
            out.sort(function (a, b) {
                var ea = vm.variantEdits[a.id] || {};
                var eb = vm.variantEdits[b.id] || {};
                if (key === "sku") return (ea.sku || a.sku || "").localeCompare(eb.sku || b.sku || "");
                if (key === "status") return (ea.status || a.status || "").localeCompare(eb.status || b.status || "");
                if (key === "price") return (Number(ea.basePriceAmount) || 0) - (Number(eb.basePriceAmount) || 0);
                return (Number(ea.sortOrder) || 0) - (Number(eb.sortOrder) || 0) || (a.id || "").localeCompare(b.id || "");
            });
            vm.variantDisplayList = out;
            vm.variantGridScrollTop = 0;
            vm.updateVariantGridLayout();
        };

        vm.clearPriceUndo = function () {
            if (vm.priceUndoTimer) $timeout.cancel(vm.priceUndoTimer);
            vm.priceUndoTimer = null;
            vm.priceUndoSnapshot = null;
        };

        vm.undoBulkPrice = function () {
            if (!vm.priceUndoSnapshot || !vm.productId) return;
            vm.error = null;
            var snap = vm.priceUndoSnapshot;
            var rows = [];
            angular.forEach(snap, function (prev, variantId) {
                rows.push({
                    productId: vm.productId,
                    variantId: variantId,
                    basePriceAmount: Math.floor(Number(prev) || 0),
                });
            });
            if (!rows.length) {
                vm.clearPriceUndo();
                return;
            }
            if (vm.priceUndoTimer) {
                $timeout.cancel(vm.priceUndoTimer);
                vm.priceUndoTimer = null;
            }
            vm.busy = true;
            vm.forward("POST", "products/bulk", { variantPrices: rows })
                .then(function (data) {
                    var failed = (data && data.failed) || [];
                    if (failed.length) {
                        vm.error = "Undo bulk: " + failed.length + " row(s) failed. First: " + (failed[0].message || "error");
                        return;
                    }
                    vm.priceUndoSnapshot = null;
                    vm.step5Notice = "Đã revert giá trên server (POST /products/bulk với basePriceAmount cũ).";
                    if (notificationsService && notificationsService.success) {
                        notificationsService.success("Bulk price", "Undo completed — prices reverted on server.");
                    }
                    return vm.loadProductVariants();
                })
                .catch(function (e) {
                    vm.error = e.message || "Undo bulk failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.loadCategories = function () {
            vm.error = null;
            vm.busy = true;
            $http
                .post(baseApi + "/Forward", {
                    method: "GET",
                    path: "categories",
                    tenantId: vm.tenantId,
                    storeId: vm.storeId,
                })
                .then(function (res) {
                    var p = parseEnvelope(res);
                    if (p.err) {
                        vm.error = p.err;
                        return;
                    }
                    vm.categories = (p.data && p.data.items) || [];
                    var idsWithChildren = {};
                    angular.forEach(vm.categories, function (c) {
                        if (c.parentId != null) idsWithChildren[c.parentId] = true;
                    });
                    angular.forEach(vm.categories, function (c) {
                        c.isLeaf = !idsWithChildren[c.id];
                    });
                    vm.leafCategories = vm.categories.filter(function (c) {
                        return c.isLeaf;
                    });
                    vm.categoryById = {};
                    angular.forEach(vm.categories, function (c) {
                        vm.categoryById[c.id] = c;
                    });
                    vm.expandedByCategoryId = {};
                })
                .catch(function (e) {
                    vm.error = (e.data && e.data.message) || e.statusText || "Request failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.primaryNameForSlug = function () {
            var v = (vm.nameVi || "").trim();
            if (v) return v;
            return (vm.nameEn || "").trim();
        };

        vm.onNameChange = function () {
            if (!vm.slugManual) vm.slug = slugify(vm.primaryNameForSlug());
            vm.scheduleSlugCheck();
        };

        vm.buildNameJson = function () {
            var o = {};
            var vi = (vm.nameVi || "").trim();
            var en = (vm.nameEn || "").trim();
            if (vi) o.vi = vi;
            if (en) o.en = en;
            return JSON.stringify(o);
        };

        vm.buildDescriptionJson = function () {
            var o = {};
            var vi = (vm.descVi || "").trim();
            var en = (vm.descEn || "").trim();
            if (vi) o.vi = vi;
            if (en) o.en = en;
            return JSON.stringify(o);
        };

        vm.slugFormatOk = function () {
            var s = (vm.slug || "").trim();
            if (!s) return null;
            return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(s);
        };

        vm.categoryLabel = function (id) {
            if (id == null) return "—";
            var found = null;
            angular.forEach(vm.categories, function (c) {
                if (c.id === id) found = c;
            });
            return found ? found.name + " (#" + found.id + ")" : "#" + id;
        };

        vm.isBranchExpanded = function (c) {
            if (!c || c.isLeaf) return true;
            return vm.expandedByCategoryId[c.id] !== false;
        };

        vm.isCategoryRowVisible = function (c) {
            if (!c || c.parentId == null) return true;
            var p = vm.categoryById[c.parentId];
            if (!p) return true;
            if (!vm.isBranchExpanded(p)) return false;
            return vm.isCategoryRowVisible(p);
        };

        function selectedIsUnderBranch(branchRootId) {
            var sid = vm.selectedCategoryId;
            if (sid == null || branchRootId == null) return false;
            var id = sid;
            while (id) {
                var node = vm.categoryById[id];
                if (!node) break;
                if (node.parentId === branchRootId) return true;
                id = node.parentId;
            }
            return false;
        }

        vm.toggleBranch = function ($event, c) {
            if ($event) {
                $event.preventDefault();
                $event.stopPropagation();
            }
            if (!c || c.isLeaf) return;
            var collapsed = vm.expandedByCategoryId[c.id] === false;
            if (collapsed) {
                delete vm.expandedByCategoryId[c.id];
            } else {
                vm.expandedByCategoryId[c.id] = false;
                if (selectedIsUnderBranch(c.id)) vm.selectedCategoryId = null;
            }
        };

        vm.expandAllCategories = function () {
            vm.expandedByCategoryId = {};
        };

        vm.collapseAllCategories = function () {
            angular.forEach(vm.categories, function (c) {
                if (!c.isLeaf) vm.expandedByCategoryId[c.id] = false;
            });
            vm.selectedCategoryId = null;
        };

        vm.axesAxisCount = function () {
            try {
                var a = JSON.parse(vm.axesJson || "[]");
                return Array.isArray(a) ? a.length : 0;
            } catch (x) {
                return 0;
            }
        };

        vm.syncAxesJsonFromPicker = function () {
            if (!vm.variantAxesDefinitions || !vm.variantAxesDefinitions.length) return;
            var axes = [];
            angular.forEach(vm.variantAxesDefinitions, function (attr) {
                var valueIds = [];
                angular.forEach(attr.values, function (v) {
                    if (vm.selectedValueIds[v.id]) valueIds.push(v.id);
                });
                if (valueIds.length) axes.push({ attributeId: attr.attributeId, valueIds: valueIds });
            });
            vm.axesJson = JSON.stringify(axes);
        };

        /** Step 4a (DAI-285): client-side cartesian preview — no API call. */
        vm.variantPreviewMaxRows = 80;
        vm.variantPreviewWarnCount = 10000;
        vm.axisPreview = { ok: false, error: null, combinationCount: 0, columns: [], rows: [], truncated: false, warnLarge: false };

        vm.computeVariantAxisPreview = function () {
            var result = {
                ok: false,
                error: null,
                combinationCount: 0,
                columns: [],
                rows: [],
                truncated: false,
                warnLarge: false,
            };

            function attrMeta(aid) {
                var found = null;
                angular.forEach(vm.variantAxesDefinitions || [], function (x) {
                    if (x.attributeId === aid) found = x;
                });
                return found;
            }

            function columnTitle(aid) {
                var a = attrMeta(aid);
                return a ? a.name : "Attr " + aid;
            }

            function cellLabel(aid, vid) {
                var a = attrMeta(aid);
                if (!a) return String(vid);
                var lbl = null;
                angular.forEach(a.values || [], function (v) {
                    if (v.id === vid) lbl = v.name;
                });
                return lbl || String(vid);
            }

            var axes;
            try {
                axes = JSON.parse(vm.axesJson || "[]");
            } catch (x) {
                result.error = "Invalid axes JSON.";
                return result;
            }
            if (!Array.isArray(axes) || axes.length === 0) {
                result.error = "No axes defined.";
                return result;
            }

            var attrIdsOrdered = [];
            var valueArrays = [];
            for (var i = 0; i < axes.length; i++) {
                var ax = axes[i];
                if (!ax || typeof ax.attributeId !== "number" || !Array.isArray(ax.valueIds) || !ax.valueIds.length) {
                    result.error = "Each axis needs numeric attributeId and non-empty valueIds.";
                    return result;
                }
                attrIdsOrdered.push(ax.attributeId);
                valueArrays.push(ax.valueIds.slice());
            }

            var combos = valueArrays.reduce(function (acc, vals) {
                var next = [];
                if (!acc.length) {
                    angular.forEach(vals, function (v) {
                        next.push([v]);
                    });
                } else {
                    angular.forEach(acc, function (row) {
                        angular.forEach(vals, function (v) {
                            next.push(row.concat([v]));
                        });
                    });
                }
                return next;
            }, []);

            result.ok = true;
            result.combinationCount = combos.length;
            result.warnLarge = combos.length > vm.variantPreviewWarnCount;
            angular.forEach(attrIdsOrdered, function (aid) {
                result.columns.push({ attributeId: aid, title: columnTitle(aid) });
            });

            var maxR = Math.min(vm.variantPreviewMaxRows, combos.length);
            for (var r = 0; r < maxR; r++) {
                var cells = [];
                for (var c = 0; c < attrIdsOrdered.length; c++) {
                    cells.push({
                        label: cellLabel(attrIdsOrdered[c], combos[r][c]),
                        valueId: combos[r][c],
                    });
                }
                result.rows.push({ cells: cells });
            }
            result.truncated = combos.length > maxR;
            return result;
        };

        $scope.$watchGroup(
            [
                function () {
                    return vm.step;
                },
                function () {
                    return vm.axesJson;
                },
                function () {
                    return vm.variantAxesDefinitions;
                },
            ],
            function () {
                if (vm.step === 4) vm.axisPreview = vm.computeVariantAxisPreview();
            }
        );

        vm.loadVariantAxesDefinitions = function () {
            return vm.forward("GET", "variant-axes", null, { skipRetryCapture: true }).then(function (data) {
                vm.variantAxesDefinitions = (data && data.items) || [];
                vm.selectedValueIds = {};
                angular.forEach(vm.variantAxesDefinitions, function (attr) {
                    angular.forEach(attr.values, function (v) {
                        vm.selectedValueIds[v.id] = false;
                    });
                });
                try {
                    var parsed = JSON.parse(vm.axesJson || "[]");
                    if (Array.isArray(parsed)) {
                        angular.forEach(parsed, function (ax) {
                            if (!ax || typeof ax.attributeId !== "number" || !Array.isArray(ax.valueIds)) return;
                            angular.forEach(ax.valueIds, function (vid) {
                                vm.selectedValueIds[vid] = true;
                            });
                        });
                    }
                } catch (x) {}
                vm.syncAxesJsonFromPicker();
            });
        };

        vm.reloadVariantAxesFromApi = function () {
            vm.loadVariantAxesDefinitions().catch(function (e) {
                vm.error = e.message || "Failed to load variant axes";
            });
        };

        vm.onSlugChange = function () {
            vm.slugManual = true;
            vm.scheduleSlugCheck();
        };

        vm.scheduleSlugCheck = function () {
            if (slugTimer) $timeout.cancel(slugTimer);
            slugTimer = $timeout(function () {
                vm.runSlugCheck();
            }, 400);
        };

        function dcmsIsNetworkishHttpError(e) {
            var s = e && e.status;
            return s === undefined || s === null || s === 0 || s === -1;
        }

        vm.dismissForwardRetry = function () {
            vm.forwardRetry = null;
        };

        vm.retryForward = function () {
            var r = vm.forwardRetry;
            if (!r) return;
            vm.error = null;
            if (r.kind === "inventory") vm.stockBusy = true;
            else vm.busy = true;
            $http
                .post(r.url, r.payload, { headers: r.headers })
                .then(function (res) {
                    vm.forwardRetry = null;
                    var p = parseEnvelope(res);
                    if (p.err) throw new Error(p.err);
                    return p.data;
                })
                .catch(function (e) {
                    var d = e.data || {};
                    if (d.error) {
                        vm.error = d.error.message || d.error.code || "Request failed";
                        return;
                    }
                    vm.error = (e && e.message) || "Retry failed";
                })
                .finally(function () {
                    if (r.kind === "inventory") vm.stockBusy = false;
                    else vm.busy = false;
                });
        };

        vm.runSlugCheck = function () {
            vm.error = null;
            var s = (vm.slug || "").trim();
            if (!s) {
                vm.slugState = null;
                return;
            }
            var cacheKey = vm.tenantId + "|" + vm.storeId + "|" + s.toLowerCase();
            var now = Date.now();
            var hit = vm.slugCache[cacheKey];
            if (hit && now - hit.ts < 60000) {
                vm.slugState = hit.available;
                return;
            }
            vm.slugPending = true;
            $http({
                method: "GET",
                url: baseApi + "/SlugCheck",
                params: { tenantId: vm.tenantId, storeId: vm.storeId, slug: s },
            })
                .then(function (res) {
                    var p = parseEnvelope(res);
                    if (p.err) {
                        vm.error = p.err;
                        vm.slugState = null;
                        return;
                    }
                    var available = p.data && p.data.available === true;
                    vm.slugState = available;
                    vm.slugCache[cacheKey] = { available: available, ts: Date.now() };
                })
                .catch(function (e) {
                    vm.error = (e.data && e.data.message) || e.statusText || "Slug check failed";
                    vm.slugState = null;
                })
                .finally(function () {
                    vm.slugPending = false;
                });
        };

        vm.forward = function (method, path, body, opts) {
            opts = opts || {};
            if (!opts.preserveError) vm.error = null;
            if (!opts.noBusy) vm.busy = true;
            var headers = { "Content-Type": "application/json" };
            var idemKey = opts.idempotencyKey || (window.crypto && crypto.randomUUID ? crypto.randomUUID() : null);
            if (idemKey) headers["Idempotency-Key"] = idemKey;
            var forwardPayload = {
                method: method,
                path: path,
                tenantId: vm.tenantId,
                storeId: vm.storeId,
                body: body,
            };
            return $http
                .post(baseApi + "/Forward", forwardPayload, { headers: headers })
                .then(function (res) {
                    vm.forwardRetry = null;
                    var p = parseEnvelope(res);
                    if (p.err) throw new Error(p.err);
                    return p.data;
                })
                .catch(function (e) {
                    if (!opts.skipRetryCapture && dcmsIsNetworkishHttpError(e)) {
                        vm.forwardRetry = {
                            kind: "catalog",
                            url: baseApi + "/Forward",
                            headers: angular.extend({}, headers),
                            payload: angular.copy(forwardPayload),
                        };
                    }
                    var d = e.data || {};
                    if (d.error) {
                        var er = new Error(d.error.message || d.error.code || "Request failed");
                        er.status = e.status;
                        er.payload = d;
                        throw er;
                    }
                    throw e;
                })
                .finally(function () {
                    if (!opts.noBusy) vm.busy = false;
                });
        };

        vm.forwardInventory = function (method, path, body, opts) {
            opts = opts || {};
            if (!opts.preserveError) vm.error = null;
            if (!opts.noBusy) vm.stockBusy = true;
            var headers = { "Content-Type": "application/json" };
            var idemKey = opts.idempotencyKey || (window.crypto && crypto.randomUUID ? crypto.randomUUID() : null);
            if (idemKey) headers["Idempotency-Key"] = idemKey;
            var forwardPayload = {
                method: method,
                path: path,
                tenantId: vm.tenantId,
                storeId: vm.storeId,
                body: body,
            };
            return $http
                .post(baseInventoryApi + "/Forward", forwardPayload, { headers: headers })
                .then(function (res) {
                    vm.forwardRetry = null;
                    var p = parseEnvelope(res);
                    if (p.err) {
                        var er0 = new Error(p.err);
                        er0.status = res.status;
                        er0.meta = p.meta;
                        throw er0;
                    }
                    if (opts.returnEnvelope) return { data: p.data, meta: p.meta };
                    return p.data;
                })
                .catch(function (e) {
                    if (!opts.skipRetryCapture && dcmsIsNetworkishHttpError(e)) {
                        vm.forwardRetry = {
                            kind: "inventory",
                            url: baseInventoryApi + "/Forward",
                            headers: angular.extend({}, headers),
                            payload: angular.copy(forwardPayload),
                        };
                    }
                    var d = e.data || {};
                    if (d.error) {
                        var er = new Error(d.error.message || d.error.code || "Request failed");
                        er.status = e.status;
                        er.meta = d.meta;
                        er.code = d.error.code;
                        er.payload = d;
                        throw er;
                    }
                    var er2 = new Error(e.statusText || "Request failed");
                    er2.status = e.status;
                    throw er2;
                })
                .finally(function () {
                    if (!opts.noBusy) vm.stockBusy = false;
                });
        };

        vm.clearStockAutoRefresh = function () {
            if (vm.stockAutoTimer) {
                window.clearInterval(vm.stockAutoTimer);
                vm.stockAutoTimer = null;
            }
        };

        vm.clearApprovalCommentsPoll = function () {
            if (vm.approvalCommentsPollTimer) {
                window.clearInterval(vm.approvalCommentsPollTimer);
                vm.approvalCommentsPollTimer = null;
            }
        };

        vm.ensureApprovalCommentsPoll = function () {
            vm.clearApprovalCommentsPoll();
            if (vm.step !== 5 || !vm.productId || !vm.productDetail) return;
            if ((vm.productDetail.status || "").toLowerCase() !== "pending_approval") return;
            vm.approvalCommentsPollTimer = window.setInterval(function () {
                if (vm.step !== 5 || !vm.productId || !vm.productDetail || (vm.productDetail.status || "").toLowerCase() !== "pending_approval") {
                    vm.clearApprovalCommentsPoll();
                    return;
                }
                $scope.$applyAsync(function () {
                    vm.loadApprovalComments();
                });
            }, 15000);
        };

        vm.approvalCommentIconClass = function (type) {
            var t = ((type || "") + "").toLowerCase();
            if (t === "approve") return "glyphicon glyphicon-ok text-success";
            if (t === "reject") return "glyphicon glyphicon-remove text-danger";
            if (t === "request_change") return "glyphicon glyphicon-warning-sign text-warning";
            if (t === "submitted") return "glyphicon glyphicon-upload text-info";
            return "glyphicon glyphicon-comment text-muted";
        };

        vm.persistPublishModeLocalOnly = function () {
            try {
                localStorage.setItem("dcmsCatalogWizard_publishMode", vm.storePublishMode === "approval" ? "approval" : "direct");
            } catch (x) {}
        };

        vm.loadStoreCatalogSettings = function () {
            vm.storeCatalogSettingsBusy = true;
            return vm
                .forward("GET", "store-catalog-settings", null, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .then(function (data) {
                    if (data && typeof data.approvalRequired === "boolean") {
                        vm.storePublishMode = data.approvalRequired ? "approval" : "direct";
                        vm.persistPublishModeLocalOnly();
                    }
                })
                .catch(function () {
                    /* keep browser default */
                })
                .finally(function () {
                    vm.storeCatalogSettingsBusy = false;
                });
        };

        vm.syncStoreCatalogApprovalRequired = function () {
            if (vm.catalogIdentity && vm.catalogIdentity.canCatalogWrite === false) return $q.resolve();
            var ar = vm.storePublishMode === "approval";
            return vm
                .forward("PATCH", "store-catalog-settings", { approvalRequired: ar }, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .catch(function () {
                    /* optional: store may deny write; workflow radio still applies locally */
                });
        };

        vm.loadCatalogIdentity = function () {
            return dcmsCatalogRbac.ensureLoaded().then(function (ctx) {
                vm.catalogIdentity = {
                    roles: ctx.roles,
                    canRunCatalogApprovalActions: ctx.canRunCatalogApprovalActions,
                    canCatalogWrite: ctx.canCatalogWrite,
                    isStoreStaff: ctx.isStoreStaff,
                };
            });
        };

        /** DAI-298: StoreStaff (catalog:read only) — hide/disable write UI in Step 5. */
        vm.readOnlyCatalogUi = function () {
            if (!vm.catalogIdentity || typeof vm.catalogIdentity.canCatalogWrite === "undefined") return false;
            return vm.catalogIdentity.canCatalogWrite === false;
        };

        vm.showApprovalActions = function () {
            if (!vm.catalogIdentity || !vm.catalogIdentity.canRunCatalogApprovalActions) return false;
            if (!vm.productDetail || !vm.productDetail.status) return false;
            return (vm.productDetail.status + "").toLowerCase() === "pending_approval";
        };

        vm.openApprovalActionModal = function (kind) {
            vm.approvalActionModal = kind;
            vm.approvalActionComment = "";
        };

        vm.closeApprovalActionModal = function () {
            vm.approvalActionModal = null;
            vm.approvalActionComment = "";
        };

        vm.confirmApprovalAction = function () {
            if (!vm.productId || !vm.approvalActionModal) return;
            var c = (vm.approvalActionComment || "").trim();
            if ((vm.approvalActionModal === "request" || vm.approvalActionModal === "reject") && !c) {
                vm.error = "Comment is required.";
                return;
            }
            vm.error = null;
            vm.busy = true;
            var path =
                vm.approvalActionModal === "request"
                    ? "products/" + vm.productId + "/request-changes"
                    : "products/" + vm.productId + "/reject";
            vm.forward("POST", path, { comment: c })
                .then(function () {
                    vm.closeApprovalActionModal();
                    vm.publishOk = true;
                    vm.step5Notice = "Approval action completed.";
                    return vm.loadProductReview();
                })
                .catch(function (e) {
                    vm.error = (e && e.message) || "Approval action failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.approvePendingProduct = function () {
            if (!vm.productId) return;
            vm.error = null;
            vm.step5Notice = null;
            vm.publishOk = false;
            vm.busy = true;
            vm.forward("POST", "products/" + vm.productId + "/approve", {})
                .then(function () {
                    vm.publishOk = true;
                    vm.step5Notice = "Product approved.";
                    return vm.loadProductReview();
                })
                .catch(function (e) {
                    vm.error = (e && e.message) || "Approve failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.ensureStockAutoRefresh = function () {
            vm.clearStockAutoRefresh();
            vm.stockAutoTimer = window.setInterval(function () {
                if (vm.step === 5 && vm.step5Tab === "stock" && vm.productId) {
                    $scope.$applyAsync(function () {
                        vm.refreshStockGrid();
                    });
                }
            }, 60000);
        };

        vm.setStep5Tab = function (t) {
            vm.step5Tab = t;
            if (t === "images") {
                vm.clearStockAutoRefresh();
                vm.onImagesTab();
            } else if (t === "stock") {
                vm.onStockTab();
            } else {
                vm.clearStockAutoRefresh();
            }
        };

        vm.loadInventoryWarehouses = function () {
            return vm
                .forwardInventory("GET", "warehouses", null, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .then(function (data) {
                    vm.inventoryWarehouses = (data && data.items) || [];
                    var ids = vm.inventoryWarehouses.map(function (w) {
                        return w.id;
                    });
                    if (vm.selectedStockWarehouseId && ids.indexOf(vm.selectedStockWarehouseId) < 0) {
                        vm.selectedStockWarehouseId = "";
                    }
                    if (!vm.selectedStockWarehouseId && vm.inventoryWarehouses.length) {
                        vm.selectedStockWarehouseId = vm.inventoryWarehouses[0].id;
                    }
                })
                .catch(function () {
                    vm.inventoryWarehouses = [];
                });
        };

        vm.refreshStockGrid = function () {
            if (!vm.productId) {
                vm.stockGridLoaded = false;
                return $q.resolve();
            }
            var variants = vm.variantsList || [];
            vm.stockBusy = true;
            vm.stockByVariant = vm.stockByVariant || {};
            if (variants.length === 0) {
                vm.stockByVariant = {};
                vm.stockGridLoaded = true;
                vm.stockBusy = false;
                return $q.resolve();
            }
            var chunk = 24;
            var chain = $q.when();
            for (var i = 0; i < variants.length; i += chunk) {
                (function (slice) {
                    chain = chain.then(function () {
                        return $q.all(
                            slice.map(function (v) {
                                var id = v.id;
                                return vm
                                    .forwardInventory("GET", "stock/variants/" + encodeURIComponent(id), null, {
                                        noBusy: true,
                                        preserveError: true,
                                        skipRetryCapture: true,
                                    })
                                    .then(function (data) {
                                        vm.stockByVariant[id] = { items: (data && data.items) || [] };
                                    })
                                    .catch(function () {
                                        vm.stockByVariant[id] = { items: [] };
                                    });
                            })
                        );
                    });
                })(variants.slice(i, i + chunk));
            }
            return chain
                .then(function () {
                    vm.stockGridLoaded = true;
                })
                .finally(function () {
                    vm.stockBusy = false;
                });
        };

        vm.stockRowMetrics = function (variant) {
            if (!vm.stockGridLoaded) {
                return { warehouseQty: null, totalAvailable: null, totalReserved: null, warn: false };
            }
            var items = (vm.stockByVariant[variant.id] && vm.stockByVariant[variant.id].items) || [];
            var totalAvail = 0;
            var totalRes = 0;
            angular.forEach(items, function (it) {
                var q = Number(it.quantity || 0);
                var r = Number(it.reservedQuantity || 0);
                var av =
                    typeof it.availableQuantity === "number" && !Number.isNaN(it.availableQuantity)
                        ? it.availableQuantity
                        : q - r;
                totalAvail += av;
                totalRes += r;
            });
            var w = vm.selectedStockWarehouseId;
            var whQty = null;
            var hasRow = false;
            if (w) {
                angular.forEach(items, function (it) {
                    if (it.warehouseId === w) {
                        hasRow = true;
                        whQty = Number(it.quantity || 0);
                    }
                });
            }
            return {
                warehouseQty: hasRow ? whQty : null,
                totalAvailable: totalAvail,
                totalReserved: totalRes,
                warn: totalAvail === 0,
            };
        };

        vm.stockTableTotals = function () {
            var tWh = 0;
            var tA = 0;
            var tR = 0;
            angular.forEach(vm.variantsList || [], function (v) {
                var m = vm.stockRowMetrics(v);
                if (m.totalAvailable !== null) {
                    tA += m.totalAvailable;
                    tR += m.totalReserved;
                    if (m.warehouseQty !== null) tWh += m.warehouseQty;
                }
            });
            return { warehouseQty: tWh, totalAvailable: tA, totalReserved: tR };
        };

        vm.stockZeroAvailableCount = function () {
            if (!vm.stockGridLoaded) return null;
            var n = 0;
            angular.forEach(vm.variantsList || [], function (v) {
                if (vm.stockRowMetrics(v).warn) n++;
            });
            return n;
        };

        vm.openStockAdjustModal = function (variant) {
            var w = vm.selectedStockWarehouseId;
            if (!w) {
                vm.error = "Select a warehouse first.";
                return;
            }
            var m = vm.stockRowMetrics(variant);
            if (m.warehouseQty === null) {
                vm.error =
                    "No stock row for this variant in the selected warehouse (seed VariantStock for tenant/store or choose another warehouse).";
                return;
            }
            vm.error = null;
            vm.stockAdjustModal = { variant: variant, warehouseId: w, currentQty: m.warehouseQty };
            vm.stockAdjustDelta = 0;
            vm.stockAdjustType = "adjustment";
            vm.stockAdjustNote = "";
        };

        vm.closeStockAdjustModal = function () {
            vm.stockAdjustModal = null;
        };

        vm.stockAdjustPreviewNewQty = function () {
            if (!vm.stockAdjustModal) return null;
            var d = Number(vm.stockAdjustDelta);
            if (Number.isNaN(d)) return null;
            return vm.stockAdjustModal.currentQty + d;
        };

        vm.stockAdjustConfirmDisabled = function () {
            if (!vm.stockAdjustModal) return true;
            var d = Number(vm.stockAdjustDelta);
            if (Number.isNaN(d) || d === 0) return true;
            var n = vm.stockAdjustPreviewNewQty();
            return n === null || n < 0;
        };

        vm.confirmStockAdjust = function () {
            if (vm.stockAdjustConfirmDisabled()) return;
            var modal = vm.stockAdjustModal;
            var d = Number(vm.stockAdjustDelta);
            vm.busy = true;
            vm.error = null;
            vm.forwardInventory("POST", "stock/adjust", {
                variantId: modal.variant.id,
                warehouseId: modal.warehouseId,
                delta: Math.trunc(d),
                movementType: vm.stockAdjustType,
                note: (vm.stockAdjustNote || "").trim() || undefined,
                createdBy: "wizard",
            })
                .then(function () {
                    if (notificationsService && notificationsService.success) {
                        notificationsService.success("Stock", "Adjust saved.");
                    }
                    vm.closeStockAdjustModal();
                    return vm.refreshStockGrid();
                })
                .catch(function (e) {
                    var st = e.status;
                    if (st === 409) {
                        if (notificationsService && notificationsService.success) {
                            notificationsService.success("Stock", "Concurrent update — table refreshed.");
                        }
                        vm.closeStockAdjustModal();
                        vm.refreshStockGrid();
                        return;
                    }
                    if (st === 422 && e.meta && e.meta.requested != null) {
                        if (notificationsService && notificationsService.error) {
                            notificationsService.error(
                                "Insufficient stock",
                                "Requested " +
                                    e.meta.requested +
                                    ", available " +
                                    (e.meta.available != null ? e.meta.available : "—")
                            );
                        }
                        return;
                    }
                    vm.error = (e && e.message) || "Adjust failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.onBulkStockCsvFiles = function (files) {
            vm.bulkStockParseError = null;
            vm.bulkStockLastResult = null;
            if (!files || !files.length) return;
            var f = files[0];
            var reader = new FileReader();
            reader.onload = function () {
                $scope.$apply(function () {
                    vm._parseBulkStockText(reader.result || "");
                });
            };
            reader.onerror = function () {
                $scope.$apply(function () {
                    vm.bulkStockParseError = "Failed to read file.";
                });
            };
            reader.readAsText(f);
        };

        vm._parseBulkStockText = function (text) {
            vm.bulkStockRows = [];
            vm.bulkStockParseError = null;
            if (!window.Papa) {
                vm.bulkStockParseError = "Papa Parse is not loaded.";
                return;
            }
            var r = Papa.parse(text || "", {
                header: true,
                skipEmptyLines: "greedy",
                transformHeader: function (h) {
                    return (h || "").trim();
                },
            });
            if (r.errors && r.errors.length) {
                vm.bulkStockParseError = r.errors
                    .map(function (e) {
                        return e.message;
                    })
                    .join("; ");
                return;
            }
            var rows = r.data || [];
            angular.forEach(rows, function (row) {
                var sku = (row.SKU != null ? row.SKU : row.sku) || "";
                var wh = (row.WarehouseId != null ? row.WarehouseId : row.warehouseId) || "";
                var deltaRaw = row.Delta != null ? row.Delta : row.delta;
                var typeRaw = (row.Type != null ? row.Type : row.type) || "adjustment";
                vm.bulkStockRows.push({
                    sku: String(sku).trim(),
                    warehouseId: String(wh).trim(),
                    deltaStr: deltaRaw == null ? "" : String(deltaRaw),
                    type: String(typeRaw).trim().toLowerCase(),
                    errors: [],
                });
            });
            vm.bulkStockValidateAllRows();
        };

        vm.bulkStockResolveVariantId = function (sku) {
            var s = (sku || "").trim().toLowerCase();
            if (!s) return null;
            var found = null;
            angular.forEach(vm.variantsList || [], function (v) {
                var e = vm.variantEdits[v.id] || {};
                var viSku = (e.sku != null ? e.sku : v.sku) || "";
                if (String(viSku).trim().toLowerCase() === s) found = v.id;
            });
            return found;
        };

        vm.bulkStockValidateAllRows = function () {
            var types = { import: true, adjustment: true, return: true };
            angular.forEach(vm.bulkStockRows, function (row) {
                row.errors = [];
                row.variantId = vm.bulkStockResolveVariantId(row.sku);
                if (!row.sku) row.errors.push("SKU required");
                else if (!row.variantId) row.errors.push("Unknown SKU for this product");
                if (row.deltaStr === "" || row.deltaStr === null) row.errors.push("Delta required");
                else {
                    var d = parseInt(String(row.deltaStr).trim(), 10);
                    if (Number.isNaN(d)) row.errors.push("Delta must be an integer");
                    else row.delta = d;
                }
                if (!row.warehouseId) row.errors.push("WarehouseId required");
                if (!types[row.type]) row.errors.push("Type must be import, adjustment, or return");
            });
        };

        vm.bulkStockRowInvalid = function (row) {
            return row.errors && row.errors.length > 0;
        };

        vm.bulkStockPreviewAllValid = function () {
            if (!vm.bulkStockRows || !vm.bulkStockRows.length) return false;
            var ok = true;
            angular.forEach(vm.bulkStockRows, function (r) {
                if (vm.bulkStockRowInvalid(r)) ok = false;
            });
            return ok;
        };

        vm.submitBulkStockCsv = function () {
            vm.bulkStockValidateAllRows();
            if (!vm.bulkStockPreviewAllValid()) {
                vm.error = "Fix CSV row errors before confirming.";
                return;
            }
            if (!vm.productId) return;
            vm.bulkStockLastResult = null;
            vm.error = null;
            vm.busy = true;
            var idem = window.crypto && crypto.randomUUID ? crypto.randomUUID() : String(Date.now());
            var items = [];
            angular.forEach(vm.bulkStockRows, function (row) {
                items.push({
                    op: "adjust",
                    variantId: row.variantId,
                    warehouseId: row.warehouseId,
                    delta: row.delta,
                    movementType: row.type,
                    createdBy: "wizard",
                });
            });
            vm.forwardInventory("POST", "stock/bulk", { createdBy: "wizard", items: items }, {
                idempotencyKey: idem,
                returnEnvelope: true,
                noBusy: true,
                preserveError: true,
            })
                .then(function (env) {
                    vm.bulkStockLastResult = { data: env.data, meta: env.meta, idempotencyKey: idem };
                    var m = env.meta || {};
                    if (notificationsService && notificationsService.success) {
                        notificationsService.success(
                            "Bulk stock",
                            "Succeeded " + (m.succeeded != null ? m.succeeded : "?") + " / failed " + (m.failed != null ? m.failed : "?") + " (requested " + (m.requested != null ? m.requested : "?") + ")."
                        );
                    }
                    return vm.refreshStockGrid();
                })
                .catch(function (e) {
                    vm.error = (e && e.message) || "Bulk stock failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.downloadBulkStockFailedCsv = function () {
            var br = vm.bulkStockLastResult;
            if (!br || !br.data || !br.data.failed || !br.data.failed.length) return;
            function esc(c) {
                var s = c == null ? "" : String(c);
                if (/[",\n\r]/.test(s)) return '"' + s.replace(/"/g, '""') + '"';
                return s;
            }
            var lines = [["index", "sku", "warehouseId", "delta", "type", "code", "message"].join(",")];
            angular.forEach(br.data.failed, function (f) {
                var row = vm.bulkStockRows[f.index] || {};
                lines.push(
                    [f.index, esc(row.sku), esc(row.warehouseId), row.delta != null ? row.delta : "", esc(row.type), esc(f.code), esc(f.message)].join(",")
                );
            });
            var blob = new Blob(["\ufeff" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
            var url = URL.createObjectURL(blob);
            var a = document.createElement("a");
            a.href = url;
            a.download = "bulk-stock-failed.csv";
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        };

        vm.onStockTab = function () {
            vm.ensureStockAutoRefresh();
            vm.loadInventoryWarehouses().then(function () {
                return vm.refreshStockGrid();
            });
        };

        vm.updateVariantGridLayout = function () {
            var items = vm.variantDisplayList || [];
            var total = items.length;
            var rowH = vm.variantRowHeight;
            var buf = vm.variantGridBuffer;
            if (total === 0) {
                vm.variantGridLayout = { start: 0, end: 0, offsetY: 0, innerH: 0 };
                vm.visibleVariantRows = [];
                return;
            }
            if (total <= 100) {
                vm.variantGridLayout = { start: 0, end: total, offsetY: 0, innerH: total * rowH };
                vm.visibleVariantRows = items.slice();
                return;
            }
            var start = Math.max(0, Math.floor(vm.variantGridScrollTop / rowH) - buf);
            var viewRows = Math.ceil(vm.variantGridViewportPx / rowH) + 2 * buf;
            var end = Math.min(total, start + viewRows);
            vm.variantGridLayout = { start: start, end: end, offsetY: start * rowH, innerH: total * rowH };
            vm.visibleVariantRows = items.slice(start, end);
        };

        vm.setVariantGridScroll = function (scrollTop) {
            vm.variantGridScrollTop = scrollTop;
            vm.updateVariantGridLayout();
        };

        vm.loadProductVariants = function () {
            if (!vm.productId) return $q.resolve();
            vm.variantsGridSkeleton = true;
            return vm
                .forward("GET", "products/" + vm.productId + "/variants", null, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .then(function (data) {
                    var items = (data && data.items) || [];
                    items.sort(function (a, b) {
                        return (a.sortOrder || 0) - (b.sortOrder || 0);
                    });
                    vm.variantsList = items;
                    vm.variantsBaseline = {};
                    vm.variantEdits = {};
                    vm.selectedVariantIds = {};
                    angular.forEach(items, function (v) {
                        var so = Number(v.sortOrder) || 0;
                        var pr = Number(v.basePriceAmount);
                        if (Number.isNaN(pr) || pr < 0) pr = 0;
                        vm.variantsBaseline[v.id] = { sku: v.sku, status: v.status, sortOrder: so, basePriceAmount: pr };
                        vm.variantEdits[v.id] = { sku: v.sku, status: v.status, sortOrder: so, basePriceAmount: pr };
                    });
                    vm.variantGridScrollTop = 0;
                    vm.variantHashConflictId = null;
                    vm.variantHashConflictHash = null;
                    vm.stockByVariant = {};
                    vm.stockGridLoaded = false;
                    vm.bulkStockRows = [];
                    vm.bulkStockLastResult = null;
                    vm.bulkStockParseError = null;
                    vm.recomputeVariantDisplayList();
                })
                .then(function () {
                    return vm.loadProductReview();
                })
                .finally(function () {
                    vm.variantsGridSkeleton = false;
                });
        };

        vm.clearVariantHashConflict = function () {
            vm.variantHashConflictId = null;
            vm.variantHashConflictHash = null;
        };

        vm.createManualVariant = function () {
            vm.error = null;
            vm.clearVariantHashConflict();
            if (!vm.productId) {
                vm.error = "Missing product id.";
                return;
            }
            var h = (vm.newManualVariant.combinationHash || "").trim().toLowerCase();
            if (h.length !== 64) {
                vm.error = "combinationHash must be 64 hex chars (SHA-256 of canonical matrix key).";
                return;
            }
            vm.busy = true;
            vm.forward("POST", "products/" + vm.productId + "/variants", {
                sku: (vm.newManualVariant.sku || "").trim(),
                combinationHash: h,
                combinationCanonical: (vm.newManualVariant.combinationCanonical || "").trim(),
                basePriceAmount: Number(vm.newManualVariant.basePriceAmount) || 0,
                status: vm.newManualVariant.status || "active",
            })
                .then(function () {
                    vm.newManualVariant = { sku: "", combinationHash: "", combinationCanonical: "", basePriceAmount: 0, status: "active" };
                    vm.step5Notice = "Variant created.";
                    return vm.loadProductVariants();
                })
                .catch(function (e) {
                    var st = e.status;
                    var d = e.payload || {};
                    if (st === 409 && d.error && d.error.code === "duplicate_combination_hash" && d.meta && d.meta.conflictingVariantId) {
                        vm.variantHashConflictId = d.meta.conflictingVariantId;
                        vm.variantHashConflictHash = (d.meta.combinationHash || h || "").toLowerCase();
                        var baseMsg = d.error.message || "Duplicate combination hash.";
                        vm.error = baseMsg + " Conflicting variant id: " + d.meta.conflictingVariantId + ".";
                    } else if (d.error) vm.error = d.error.message || d.error.code || "Create variant failed";
                    else vm.error = e.message || "Create variant failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.loadApprovalComments = function () {
            if (!vm.productId) {
                vm.approvalComments = [];
                return $q.resolve();
            }
            return vm
                .forward("GET", "products/" + vm.productId + "/approval-comments", null, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .then(function (data) {
                    vm.approvalComments = (data && data.items) || [];
                    $timeout(function () {
                        var el = document.getElementById("dcmsApprovalTimeline");
                        if (el) el.scrollTop = el.scrollHeight;
                    }, 0);
                })
                .catch(function () {
                    vm.approvalComments = [];
                });
        };

        vm.loadProductReview = function () {
            if (!vm.productId) {
                vm.productDetail = null;
                vm.approvalComments = [];
                vm.clearApprovalCommentsPoll();
                return $q.resolve();
            }
            return vm
                .forward("GET", "products/" + vm.productId, null, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .then(function (data) {
                    vm.productDetail = data;
                    return vm.loadApprovalComments();
                })
                .then(function () {
                    vm.ensureApprovalCommentsPoll();
                })
                .catch(function () {
                    vm.productDetail = null;
                    vm.approvalComments = [];
                    vm.clearApprovalCommentsPoll();
                });
        };

        vm.reviewActiveVariantCount = function () {
            var n = 0;
            angular.forEach(vm.variantsList, function (v) {
                var e = vm.variantEdits[v.id];
                if (e && (e.status || "").toLowerCase() === "active") n++;
            });
            return n;
        };

        vm.reviewInactiveVariantCount = function () {
            var n = 0;
            angular.forEach(vm.variantsList, function (v) {
                var e = vm.variantEdits[v.id];
                if (e && (e.status || "").toLowerCase() !== "active") n++;
            });
            return n;
        };

        vm.persistPublishMode = function () {
            vm.persistPublishModeLocalOnly();
            vm.syncStoreCatalogApprovalRequired();
        };

        vm.postApprovalReply = function () {
            if (!vm.productId) return;
            var msg = (vm.approvalReplyText || "").trim();
            if (!msg) {
                vm.error = "Enter a message to add to the approval timeline.";
                return;
            }
            vm.error = null;
            vm.busy = true;
            vm.forward("POST", "products/" + vm.productId + "/approval-comments", { message: msg })
                .then(function () {
                    vm.approvalReplyText = "";
                    vm.step5Notice = "Comment added to the approval timeline.";
                    return vm.loadApprovalComments();
                })
                .catch(function (e) {
                    vm.error = (e && e.message) || "Failed to post comment";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.persistProductUrlTemplate = function () {
            try {
                localStorage.setItem("dcmsCatalogWizard_productUrlTemplate", vm.productUrlTemplate || "");
            } catch (x) {}
        };

        vm.maybeOpenProductDetail = function () {
            var t = (vm.productUrlTemplate || "").trim();
            if (!t || !vm.productId) return;
            var url = t
                .split("{{productId}}")
                .join(vm.productId)
                .split("{{tenantId}}")
                .join(vm.tenantId || "")
                .split("{{storeId}}")
                .join(vm.storeId || "");
            window.open(url, "_blank", "noopener,noreferrer");
        };

        vm.openProductDetailNow = function () {
            vm.maybeOpenProductDetail();
        };

        vm._buildVariantEditJobs = function () {
            var jobs = [];
            if (!vm.productId) return { jobs: jobs, validationError: "Missing product id." };
            var err = null;
            angular.forEach(vm.variantsList, function (v) {
                var e = vm.variantEdits[v.id];
                var b = vm.variantsBaseline[v.id];
                if (!e || !b) return;
                var so = Number(e.sortOrder);
                var pr = Number(e.basePriceAmount);
                if (Number.isNaN(so) || so < 0) err = "Sort order must be a non-negative number.";
                if (Number.isNaN(pr) || pr < 0) err = "Base price must be a non-negative number.";
            });
            if (err) return { jobs: [], validationError: err };
            angular.forEach(vm.variantsList, function (v) {
                var e = vm.variantEdits[v.id];
                var b = vm.variantsBaseline[v.id];
                if (!e || !b) return;
                var so = Number(e.sortOrder);
                var pr = Number(e.basePriceAmount);
                if (Number.isNaN(pr) || pr < 0) pr = 0;
                if (e.sku === b.sku && e.status === b.status && so === b.sortOrder && pr === b.basePriceAmount) return;
                jobs.push({
                    id: v.id,
                    body: { sku: (e.sku || "").trim(), status: e.status, sortOrder: so, basePriceAmount: pr },
                });
            });
            return { jobs: jobs, validationError: null };
        };

        vm._runVariantPutJobs = function (jobs) {
            if (!jobs.length) return $q.resolve();
            vm.variantSaveProgress = { done: 0, total: jobs.length };
            var chain = $q.when();
            angular.forEach(jobs, function (job) {
                chain = chain.then(function () {
                    vm.variantRowSaving[job.id] = true;
                    vm.variantRowFlash[job.id] = null;
                    return vm
                        .forward("PUT", "products/" + vm.productId + "/variants/" + job.id, job.body, { noBusy: true, skipRetryCapture: true })
                        .then(function () {
                            vm.variantSaveProgress.done++;
                            vm.variantRowFlash[job.id] = "ok";
                            $timeout(function () {
                                vm.variantRowFlash[job.id] = null;
                            }, 900);
                        })
                        .catch(function (e) {
                            vm.variantRowFlash[job.id] = "err";
                            $timeout(function () {
                                vm.variantRowFlash[job.id] = null;
                            }, 1400);
                            throw e;
                        })
                        .finally(function () {
                            vm.variantRowSaving[job.id] = false;
                        });
                });
            });
            return chain.then(function () {
                return vm.loadProductVariants();
            });
        };

        vm.applyBulkVariantPrices = function () {
            vm.error = null;
            vm.step5Notice = null;
            vm.clearPriceUndo();
            var price = Number(vm.bulkPriceInput);
            if (Number.isNaN(price) || price < 0) {
                vm.error = "Bulk price must be a non-negative number.";
                return;
            }
            var rows = [];
            angular.forEach(vm.selectedVariantIds, function (sel, id) {
                if (!sel || !vm.variantEdits[id]) return;
                rows.push({ productId: vm.productId, variantId: id, basePriceAmount: Math.floor(price) });
            });
            if (!rows.length) {
                vm.error = "Select at least one variant row.";
                return;
            }
            var lines = [];
            var snap = {};
            angular.forEach(rows, function (r) {
                var e = vm.variantEdits[r.variantId];
                var prev = e ? Number(e.basePriceAmount) || 0 : 0;
                snap[r.variantId] = prev;
                lines.push(r.variantId.slice(-8) + "… : " + prev + " → " + r.basePriceAmount);
            });
            var msg = "Apply base price " + Math.floor(price) + " to " + rows.length + " variant(s)?\n\n" + lines.join("\n");
            if (!window.confirm(msg)) return;
            vm.busy = true;
            vm.forward("POST", "products/bulk", { variantPrices: rows })
                .then(function (data) {
                    var failed = (data && data.failed) || [];
                    if (failed.length) {
                        vm.error = "Bulk price: " + failed.length + " row(s) failed. First: " + (failed[0].message || "error");
                        return;
                    }
                    angular.forEach(rows, function (r) {
                        if (vm.variantEdits[r.variantId]) vm.variantEdits[r.variantId].basePriceAmount = r.basePriceAmount;
                        if (vm.variantsBaseline[r.variantId]) vm.variantsBaseline[r.variantId].basePriceAmount = r.basePriceAmount;
                        angular.forEach(vm.variantsList, function (x) {
                            if (x.id === r.variantId) x.basePriceAmount = r.basePriceAmount;
                        });
                    });
                    vm.recomputeVariantDisplayList();
                    vm.priceUndoSnapshot = snap;
                    vm.step5Notice =
                        "Đã lưu giá bulk. Trong 10s có thể Undo: gọi lại POST /products/bulk với basePriceAmount cũ từng variant (revert trên server).";
                    if (notificationsService && notificationsService.success) {
                        notificationsService.success(
                            "Bulk price",
                            "Saved. Undo available for 10s (use Undo bulk button below)."
                        );
                    }
                    vm.priceUndoTimer = $timeout(function () {
                        vm.clearPriceUndo();
                    }, 10000);
                })
                .catch(function (e) {
                    vm.error = e.message || "Bulk price failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.loadProductImages = function () {
            if (!vm.productId) return $q.resolve();
            vm.imageBusy = true;
            return vm
                .forward("GET", "products/" + vm.productId + "/images", null, { noBusy: true, preserveError: true, skipRetryCapture: true })
                .then(function (data) {
                    vm.imagesList = (data && data.items) || [];
                })
                .catch(function () {
                    vm.imagesList = [];
                })
                .finally(function () {
                    vm.imageBusy = false;
                });
        };

        vm.onImagesTab = function () {
            vm.loadProductImages();
        };

        vm.uploadProductImageFiles = function (files) {
            if (!vm.productId || !files || !files.length) return;
            vm.error = null;
            var chain = $q.when();
            angular.forEach(files, function (file) {
                chain = chain.then(function () {
                    return new $q(function (resolve, reject) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            resolve(reader.result);
                        };
                        reader.onerror = function () {
                            reject(new Error("Failed to read file."));
                        };
                        reader.readAsArrayBuffer(file);
                    }).then(function (buf) {
                        return vm.sha256HexOfArrayBuffer(buf).then(function (hex) {
                            return vm.forward("GET", "products/" + vm.productId + "/images/checksum-check?checksum=" + encodeURIComponent(hex), null, {
                                noBusy: true,
                                preserveError: true,
                                skipRetryCapture: true,
                            }).then(function (chk) {
                                if (chk && chk.exists) {
                                    if (!window.confirm("Duplicate checksum on this product. Register another copy anyway?")) {
                                        return $q.resolve();
                                    }
                                }
                                return vm
                                    .forward(
                                        "POST",
                                        "products/" + vm.productId + "/images/presign",
                                        {
                                            checksumSha256: hex,
                                            contentType: file.type || "application/octet-stream",
                                            fileName: file.name,
                                            imageType: "gallery",
                                        },
                                        { noBusy: true }
                                    )
                                    .then(function (pre) {
                                        var ct = (pre.headers && pre.headers["Content-Type"]) || file.type || "application/octet-stream";
                                        var hdrs = pre.headers ? angular.copy(pre.headers) : { "Content-Type": ct };
                                        if (pre.uploadUrl && /^https?:\/\//i.test(pre.uploadUrl)) {
                                            return fetch(pre.uploadUrl, {
                                                method: "PUT",
                                                body: buf,
                                                headers: hdrs,
                                            }).then(function (res) {
                                                if (!res.ok) throw new Error("S3 upload failed (" + res.status + ").");
                                                return vm
                                                    .forward(
                                                        "POST",
                                                        "products/" + vm.productId + "/images/" + pre.imageId + "/s3-complete",
                                                        { contentLength: file.size },
                                                        { noBusy: true }
                                                    )
                                                    .then(function () {
                                                        return vm.loadProductImages();
                                                    });
                                            });
                                        }
                                        var path = pre.path;
                                        if (!path) throw new Error("Presign response missing path.");
                                        return vm.forwardBinaryPut(path, buf, ct).then(function () {
                                            return vm.loadProductImages();
                                        });
                                    });
                            });
                        });
                    });
                });
            });
            vm.busy = true;
            chain.catch(function (e) {
                vm.error = (e && e.message) || "Image upload failed";
            }).finally(function () {
                vm.busy = false;
            });
        };

        vm.patchImage = function (imageId, body) {
            return vm.forward("PATCH", "products/" + vm.productId + "/images/" + imageId, body, { noBusy: true, skipRetryCapture: true }).then(function () {
                return vm.loadProductImages();
            });
        };

        vm.setImagePrimary = function (imageId) {
            vm.error = null;
            vm.patchImage(imageId, { isPrimary: true }).catch(function (e) {
                vm.error = e.message || "Failed to set primary";
            });
        };

        vm.cycleImageType = function (imageId) {
            vm.error = null;
            vm.patchImage(imageId, { cycleType: true }).catch(function (e) {
                vm.error = e.message || "Failed to update type";
            });
        };

        vm.deleteProductImage = function (imageId) {
            vm.error = null;
            if (!window.confirm("Remove this image from the product (blob file may remain on disk/S3)?")) return;
            vm.busy = true;
            vm.forward("DELETE", "products/" + vm.productId + "/images/" + imageId, {})
                .then(function () {
                    return vm.loadProductImages();
                })
                .catch(function (e) {
                    vm.error = e.message || "Delete failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm._commitImageOrder = function (ids) {
            vm.busy = true;
            return vm
                .forward("PATCH", "products/" + vm.productId + "/images/order", { imageIds: ids })
                .then(function () {
                    return vm.loadProductImages();
                })
                .catch(function (e) {
                    vm.error = e.message || "Reorder failed";
                })
                .finally(function () {
                    vm.busy = false;
                });
        };

        vm.moveImageUp = function (index) {
            if (index <= 0) return;
            var ids = vm.imagesList.map(function (i) {
                return i.id;
            });
            var t = ids[index - 1];
            ids[index - 1] = ids[index];
            ids[index] = t;
            vm._commitImageOrder(ids);
        };

        vm.moveImageDown = function (index) {
            if (index >= vm.imagesList.length - 1) return;
            var ids = vm.imagesList.map(function (i) {
                return i.id;
            });
            var t = ids[index + 1];
            ids[index + 1] = ids[index];
            ids[index] = t;
            vm._commitImageOrder(ids);
        };

        /** DAI-291: drag row onto another row (HTML5 DnD); same PATCH /images/order as ↑/↓. */
        vm.commitImageReorderDrop = function (draggedId, dropIndex) {
            if (!vm.productId || !vm.imagesList || vm.imagesList.length < 2) return;
            var ids = vm.imagesList.map(function (i) {
                return i.id;
            });
            var from = ids.indexOf(draggedId);
            if (from < 0 || dropIndex < 0 || dropIndex >= ids.length) return;
            if (from === dropIndex) return;
            var next = ids.slice();
            var moved = next.splice(from, 1)[0];
            next.splice(dropIndex, 0, moved);
            vm._commitImageOrder(next);
        };

        vm.selectAllVariants = function (on) {
            angular.forEach(vm.variantsList, function (v) {
                vm.selectedVariantIds[v.id] = !!on;
            });
        };

        vm.selectVisibleVariants = function () {
            angular.forEach(vm.visibleVariantRows, function (v) {
                vm.selectedVariantIds[v.id] = true;
            });
        };

        vm.clearVariantSelection = function () {
            vm.selectedVariantIds = {};
        };

        vm.applyBulkStatusToSelection = function () {
            var st = (vm.bulkStatusTarget || "active").toLowerCase();
            angular.forEach(vm.selectedVariantIds, function (sel, id) {
                if (sel && vm.variantEdits[id]) vm.variantEdits[id].status = st;
            });
        };

        vm.selectedVariantCount = function () {
            var n = 0;
            angular.forEach(vm.selectedVariantIds, function (sel) {
                if (sel) n++;
            });
            return n;
        };

        /** Assign sortOrder = bulkSortStart, bulkSortStart+1, … to selected rows (variantsList order). */
        vm.applyBulkSortIncrementToSelection = function () {
            vm.error = null;
            var base = Number(vm.bulkSortStart);
            if (Number.isNaN(base) || base < 0) {
                vm.error = "Bulk sort start must be a non-negative number.";
                return;
            }
            var n = base;
            var any = false;
            angular.forEach(vm.variantsList, function (v) {
                if (vm.selectedVariantIds[v.id] && vm.variantEdits[v.id]) {
                    vm.variantEdits[v.id].sortOrder = n++;
                    any = true;
                }
            });
            if (!any) vm.error = "No rows selected for bulk sort.";
        };

        vm.saveVariantEdits = function () {
            vm.error = null;
            vm.step5Notice = null;
            var r = vm._buildVariantEditJobs();
            if (r.validationError) {
                vm.error = r.validationError;
                return;
            }
            if (!r.jobs.length) {
                vm.error = "No edits to save.";
                return;
            }
            vm.busy = true;
            vm._runVariantPutJobs(r.jobs)
                .then(function () {
                    vm.error = null;
                })
                .catch(function (e) {
                    vm.error = e.message || "Save failed";
                })
                .finally(function () {
                    vm.busy = false;
                    vm.variantSaveProgress = null;
                });
        };

        vm.saveDraft = function () {
            vm.error = null;
            vm.step5Notice = null;
            vm.publishOk = false;
            var r = vm._buildVariantEditJobs();
            if (r.validationError) {
                vm.error = r.validationError;
                return;
            }
            if (!r.jobs.length) {
                vm.step5Notice =
                    "No SKU/status/sort changes to persist. Product keeps its current catalog status until you publish or submit for approval.";
                vm.loadProductReview();
                return;
            }
            vm.busy = true;
            vm._runVariantPutJobs(r.jobs)
                .then(function () {
                    vm.error = null;
                    vm.step5Notice = "Variant edits saved (catalog workflow unchanged).";
                })
                .catch(function (e) {
                    vm.error = e.message || "Save failed";
                })
                .finally(function () {
                    vm.busy = false;
                    vm.variantSaveProgress = null;
                });
        };

        vm.hideFromCatalog = function () {
            vm.error = null;
            vm.step5Notice = null;
            vm.publishOk = false;
            if (!vm.productId) return;
            vm.forward("POST", "products/" + vm.productId + "/hide", {})
                .then(function () {
                    vm.error = null;
                    vm.step5Notice = "Product set to hidden (storefront should treat as not visible when status is respected).";
                    return vm.loadProductReview();
                })
                .catch(function (e) {
                    vm.error = e.message || "Hide failed (only active products can be hidden).";
                });
        };

        vm.next = function () {
            vm.error = null;
            if (vm.readOnlyCatalogUi() && (vm.step === 2 || vm.step === 4)) {
                vm.error = "Read-only (StoreStaff): catalog writes are disabled.";
                return;
            }
            if (vm.step === 1) {
                if (!vm.selectedCategoryId) {
                    vm.error = "Select a leaf category.";
                    return;
                }
                vm.step = 2;
                vm.scheduleSlugCheck();
                return;
            }
            if (vm.step === 2) {
                if (!(vm.nameVi || "").trim()) {
                    vm.error = "Name (Tiếng Việt) is required.";
                    return;
                }
                if (!vm.slug) {
                    vm.error = "Slug is required.";
                    return;
                }
                if (vm.slugFormatOk() === false) {
                    vm.error = "Slug must use lowercase letters, digits, and single hyphens (no spaces or accents).";
                    return;
                }
                if (vm.slugState !== true) {
                    vm.error = "Slug must be available before continuing.";
                    return;
                }
                vm.busy = true;
                var nameJson = vm.buildNameJson();
                var descriptionJson = vm.buildDescriptionJson();
                vm.forward("POST", "products", {
                    categoryId: vm.selectedCategoryId,
                    nameJson: nameJson,
                    descriptionJson: descriptionJson,
                    slug: vm.slug.trim().toLowerCase(),
                })
                    .then(function (data) {
                        vm.productId = data.id;
                        return vm.loadVariantAxesDefinitions().catch(function () {
                            vm.variantAxesDefinitions = [];
                        });
                    })
                    .then(function () {
                        vm.step = 3;
                    })
                    .catch(function (e) {
                        vm.error = e.message || "Create product failed";
                    })
                    .finally(function () {
                        vm.busy = false;
                    });
                return;
            }
            if (vm.step === 3) {
                var axes;
                try {
                    axes = JSON.parse(vm.axesJson || "[]");
                } catch (x) {
                    vm.error = "Axes JSON is invalid.";
                    return;
                }
                if (!Array.isArray(axes) || axes.length === 0) {
                    vm.error = "Axes must be a non-empty JSON array.";
                    return;
                }
                for (var i = 0; i < axes.length; i++) {
                    var ax = axes[i];
                    if (!ax || typeof ax.attributeId !== "number" || !Array.isArray(ax.valueIds) || !ax.valueIds.length) {
                        vm.error = 'Each axis needs numeric attributeId and non-empty valueIds (see API).';
                        return;
                    }
                }
                vm.step = 4;
                return;
            }
            if (vm.step === 4) {
                vm.runGenerate();
                return;
            }
            if (vm.step === 5) {
                return;
            }
        };

        vm.prev = function () {
            vm.error = null;
            if (vm.step > 1) vm.step--;
        };

        vm.runGenerate = function () {
            vm.error = null;
            var axes;
            try {
                axes = JSON.parse(vm.axesJson || "[]");
            } catch (x) {
                vm.error = "Axes JSON invalid.";
                return;
            }
            if (!vm.productId) {
                vm.error = "Missing product id.";
                return;
            }
            vm.forward("POST", "products/" + vm.productId + "/variants/generate", {
                axes: axes,
                skuPrefix: vm.skuPrefix || "sku",
            })
                .then(function (data) {
                    vm.generateResult = data;
                    vm.publishOk = false;
                    vm.step5Notice = null;
                    vm.step = 5;
                    return vm.loadProductVariants().catch(function (e2) {
                        vm.error = (e2 && e2.message) || "Failed to load variants after generate.";
                    });
                })
                .catch(function (e) {
                    vm.error = e.message || "Generate failed";
                });
        };

        vm.publish = function () {
            if (!vm.productId) return;
            vm.publishOk = false;
            vm.step5Notice = null;
            vm.forward("POST", "products/" + vm.productId + "/publish", {})
                .then(function () {
                    vm.error = null;
                    vm.publishOk = true;
                    vm.step = 5;
                    return vm.loadProductReview().then(function () {
                        vm.maybeOpenProductDetail();
                    });
                })
                .catch(function (e) {
                    var d = (e && e.payload) || {};
                    var code = d.error && d.error.code;
                    if (code === "approval_required") {
                        vm.error =
                            d.error.message ||
                            "This store requires submit-for-approval before publish (unless you use an elevated catalog role).";
                    } else vm.error = e.message || "Publish failed";
                });
        };

        vm.submitApproval = function () {
            if (!vm.productId) return;
            vm.publishOk = false;
            vm.step5Notice = null;
            vm.forward("POST", "products/" + vm.productId + "/submit-for-approval", {})
                .then(function () {
                    vm.error = null;
                    vm.publishOk = true;
                    return vm.loadProductReview().then(function () {
                        vm.maybeOpenProductDetail();
                    });
                })
                .catch(function (e) {
                    vm.error = e.message || "Submit failed";
                });
        };

        $scope.$watchGroup(
            [
                function () {
                    return vm.variantFilterText;
                },
                function () {
                    return vm.variantSortKey;
                },
            ],
            function () {
                if (vm.step === 5) vm.recomputeVariantDisplayList();
            }
        );

        $scope.$watch(
            function () {
                return vm.step;
            },
            function (n) {
                if (n !== 5) vm.clearApprovalCommentsPoll();
            }
        );

        $scope.$watchGroup(
            [
                function () {
                    return vm.tenantId;
                },
                function () {
                    return vm.storeId;
                },
            ],
            function () {
                try {
                    if (vm.tenantId) localStorage.setItem("dcmsBell_tenantId", vm.tenantId);
                    if (vm.storeId) localStorage.setItem("dcmsBell_storeId", vm.storeId);
                } catch (x) {}
                vm.loadStoreCatalogSettings();
            }
        );

        $scope.$on("$destroy", function () {
            vm.clearStockAutoRefresh();
            vm.clearApprovalCommentsPoll();
        });

        /** DAI-299: global 409 from $http interceptor — refresh wizard data on Step 5. */
        $scope.$on("dcms:refreshCatalogData", function () {
            if (vm.step !== 5 || !vm.productId) return;
            vm.loadApprovalComments().catch(function () {});
            vm.loadProductImages().catch(function () {});
            vm.loadProductVariants().catch(function () {});
            vm.refreshStockGrid();
        });

        vm.loadCatalogIdentity();
        vm.loadStoreCatalogSettings();
        vm.loadCategories();
        if (openNavProduct && vm.productId) {
            vm.loadProductReview();
            vm.loadProductVariants().catch(function () {});
            vm.loadProductImages().catch(function () {});
        }
    },
]);

angular.module("umbraco").directive("dcmsProductWizardScroll", function () {
    return {
        restrict: "A",
        scope: false,
        link: function (scope, element) {
            var el = element[0];
            function onScroll() {
                var st = el.scrollTop;
                scope.$applyAsync(function () {
                    if (scope.vm && typeof scope.vm.setVariantGridScroll === "function") scope.vm.setVariantGridScroll(st);
                });
            }
            element.on("scroll", onScroll);
            scope.$on("$destroy", function () {
                element.off("scroll", onScroll);
            });
        },
    };
});

angular.module("umbraco").directive("dcmsBindFiles", function () {
    return {
        restrict: "A",
        scope: false,
        link: function (scope, element, attrs) {
            element.on("change", function (e) {
                var fn = scope.$eval(attrs.dcmsBindFiles);
                var files = e.target && e.target.files ? e.target.files : null;
                if (typeof fn === "function") fn(files);
                try {
                    element.val(null);
                } catch (x) {}
            });
            scope.$on("$destroy", function () {
                element.off("change");
            });
        },
    };
});

angular.module("umbraco").directive("dcmsBindBulkStockCsv", function () {
    return {
        restrict: "A",
        scope: false,
        link: function (scope, element, attrs) {
            element.on("change", function (e) {
                var fn = scope.$eval(attrs.dcmsBindBulkStockCsv);
                var files = e.target && e.target.files ? e.target.files : null;
                if (typeof fn === "function") fn(files);
                try {
                    element.val(null);
                } catch (x) {}
            });
            scope.$on("$destroy", function () {
                element.off("change");
            });
        },
    };
});

/** US-14: drop image files onto dashed zone → same handler as file input (checksum / presign / S3). */
angular.module("umbraco").directive("dcmsImageFilesDropZone", function () {
    return {
        restrict: "A",
        scope: false,
        link: function (scope, element, attrs) {
            var el = element[0];
            function allowDrop(ev) {
                ev.preventDefault();
                ev.stopPropagation();
            }
            function onDrop(ev) {
                allowDrop(ev);
                var dt = ev.dataTransfer;
                if (!dt || !dt.files || !dt.files.length) return;
                var files = dt.files;
                scope.$applyAsync(function () {
                    var fn = scope.$eval(attrs.dcmsImageFilesDropZone);
                    if (typeof fn === "function") fn(files);
                });
            }
            el.addEventListener("dragenter", allowDrop);
            el.addEventListener("dragover", allowDrop);
            el.addEventListener("drop", onDrop);
            scope.$on("$destroy", function () {
                el.removeEventListener("dragenter", allowDrop);
                el.removeEventListener("dragover", allowDrop);
                el.removeEventListener("drop", onDrop);
            });
        },
    };
});

/** DAI-291: HTML5 drag handle → application/x-dcms-image-id (Umbraco AngularJS has no npm/dnd-kit bundle). */
angular.module("umbraco").directive("dcmsImageDragSource", function () {
    return {
        restrict: "A",
        link: function (scope, element, attrs) {
            var el = element[0];
            el.setAttribute("draggable", "true");
            function onDragStart(ev) {
                var id = attrs.dcmsImageDragSource || (scope.img && scope.img.id);
                if (!id) return;
                ev.dataTransfer.setData("application/x-dcms-image-id", id);
                ev.dataTransfer.effectAllowed = "move";
            }
            element.on("dragstart", onDragStart);
            scope.$on("$destroy", function () {
                element.off("dragstart", onDragStart);
            });
        },
    };
});

angular.module("umbraco").directive("dcmsImageDropRow", function () {
    return {
        restrict: "A",
        link: function (scope, element, attrs) {
            var row = element[0];
            function onDragOver(ev) {
                ev.preventDefault();
                ev.dataTransfer.dropEffect = "move";
                row.style.outline = "1px dashed #337ab7";
            }
            function onDragLeave() {
                row.style.outline = "";
            }
            function onDrop(ev) {
                ev.preventDefault();
                row.style.outline = "";
                var draggedId = ev.dataTransfer.getData("application/x-dcms-image-id");
                var dropIndex = parseInt(attrs.dcmsImageDropRow, 10);
                if (!draggedId || Number.isNaN(dropIndex)) return;
                scope.$applyAsync(function () {
                    if (scope.vm && typeof scope.vm.commitImageReorderDrop === "function") {
                        scope.vm.commitImageReorderDrop(draggedId, dropIndex);
                    }
                });
            }
            element.on("dragover", onDragOver);
            element.on("dragleave", onDragLeave);
            element.on("drop", onDrop);
            scope.$on("$destroy", function () {
                element.off("dragover", onDragOver);
                element.off("dragleave", onDragLeave);
                element.off("drop", onDrop);
            });
        },
    };
});
