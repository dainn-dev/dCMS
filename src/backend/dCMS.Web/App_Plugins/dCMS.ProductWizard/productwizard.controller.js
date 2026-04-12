/**
 * US-13 — 5-step Store Manager product wizard (AngularJS, Umbraco backoffice).
 * Uses BFF: SlugCheck (GET) + Forward (POST) → Catalog.Api.
 */
angular.module("umbraco").controller("Umbraco.dCMS.ProductWizardController", [
    "$http",
    "$timeout",
    "$scope",
    "$q",
    function ($http, $timeout, $scope, $q) {
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
        vm.step5Notice = null;
        vm.storePublishMode = "direct";
        vm.productUrlTemplate = "";
        try {
            var pm = localStorage.getItem("dcmsCatalogWizard_publishMode");
            vm.storePublishMode = pm === "approval" ? "approval" : "direct";
            vm.productUrlTemplate = localStorage.getItem("dcmsCatalogWizard_productUrlTemplate") || "";
        } catch (x) {}

        var slugTimer = null;
        var baseApi = "/umbraco/backoffice/api/DcmsCatalog/CatalogBackofficeProxy";

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
                return { err: d.error.message || d.error.code || "error", data: null };
            }
            return { err: null, data: d ? d.data : null };
        }

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
            return vm.forward("GET", "variant-axes", null).then(function (data) {
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
            if (window.crypto && crypto.randomUUID) {
                headers["Idempotency-Key"] = crypto.randomUUID();
            }
            return $http
                .post(
                    baseApi + "/Forward",
                    {
                        method: method,
                        path: path,
                        tenantId: vm.tenantId,
                        storeId: vm.storeId,
                        body: body,
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
                    if (!opts.noBusy) vm.busy = false;
                });
        };

        vm.updateVariantGridLayout = function () {
            var items = vm.variantsList || [];
            var total = items.length;
            var rowH = vm.variantRowHeight;
            var buf = vm.variantGridBuffer;
            if (total === 0) {
                vm.variantGridLayout = { start: 0, end: 0, offsetY: 0, innerH: 0 };
                vm.visibleVariantRows = [];
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
            return vm.forward("GET", "products/" + vm.productId + "/variants", null, { noBusy: true, preserveError: true }).then(function (data) {
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
                    vm.variantsBaseline[v.id] = { sku: v.sku, status: v.status, sortOrder: so };
                    vm.variantEdits[v.id] = { sku: v.sku, status: v.status, sortOrder: so };
                });
                vm.variantGridScrollTop = 0;
                vm.updateVariantGridLayout();
            }).then(function () {
                return vm.loadProductReview();
            });
        };

        vm.loadProductReview = function () {
            if (!vm.productId) {
                vm.productDetail = null;
                return $q.resolve();
            }
            return vm.forward("GET", "products/" + vm.productId, null, { noBusy: true, preserveError: true }).then(function (data) {
                vm.productDetail = data;
            }).catch(function () {
                vm.productDetail = null;
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
            try {
                localStorage.setItem("dcmsCatalogWizard_publishMode", vm.storePublishMode === "approval" ? "approval" : "direct");
            } catch (x) {}
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
                if (Number.isNaN(so) || so < 0) err = "Sort order must be a non-negative number.";
            });
            if (err) return { jobs: [], validationError: err };
            angular.forEach(vm.variantsList, function (v) {
                var e = vm.variantEdits[v.id];
                var b = vm.variantsBaseline[v.id];
                if (!e || !b) return;
                var so = Number(e.sortOrder);
                if (e.sku === b.sku && e.status === b.status && so === b.sortOrder) return;
                jobs.push({ id: v.id, body: { sku: (e.sku || "").trim(), status: e.status, sortOrder: so } });
            });
            return { jobs: jobs, validationError: null };
        };

        vm._runVariantPutJobs = function (jobs) {
            if (!jobs.length) return $q.resolve();
            vm.variantSaveProgress = { done: 0, total: jobs.length };
            var chain = $q.when();
            angular.forEach(jobs, function (job) {
                chain = chain.then(function () {
                    return vm
                        .forward("PUT", "products/" + vm.productId + "/variants/" + job.id, job.body, { noBusy: true })
                        .then(function () {
                            vm.variantSaveProgress.done++;
                        });
                });
            });
            return chain.then(function () {
                return vm.loadProductVariants();
            });
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
                    vm.error = e.message || "Publish failed";
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

        vm.loadCategories();
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
