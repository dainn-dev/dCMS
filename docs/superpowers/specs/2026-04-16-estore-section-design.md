# eStore Backoffice Section — Design Spec

**Date:** 2026-04-16  
**Status:** Approved

---

## Overview

Add a new **eStore** Umbraco backoffice section that sits next to the existing Orders section. The eStore section manages catalog and fulfillment configuration: Brands, Categories, Products, Attributes, and Fulfillment Options. It follows the exact same architecture as the Orders section (React SPA embedded in an Umbraco web component).

---

## Architecture

### Build Strategy: Second Vite Entry

The existing SPA is a single Vite library build (`src/index.tsx` → `orders-spa.js`). The eStore section adds a **second entry point** (`src/estore-index.tsx` → `estore-spa.js`) via Vite `rollupOptions.input`. This produces two fully independent bundles sharing no runtime state:

```
dist/
├── orders-spa.js        ← existing
├── orders-spa.css       ← existing
├── estore-spa.js        ← new
└── estore-spa.css       ← new
```

The two SPAs are isolated — the eStore bundle cannot break the Orders bundle and vice versa.

### Umbraco Integration

Same pattern as Orders:
1. A vanilla JS web component file (`dcms-estore-section.js`) registered as a custom element `dcms-estore-section`
2. `connectedCallback` fetches + injects CSS inline (required for Umbraco v16 Shadow DOM), then dynamically imports the SPA JS
3. Exports `mount(host)` / `unmount(host)` from `estore-index.tsx`
4. Section registered in `umbraco-package.json` with alias `dcmsEStore`, weight 940 (renders left of Orders at weight 950)
5. Backend grants the section to Admin and Editor groups on startup

---

## File Plan

### New files — Frontend

| File | Purpose |
|---|---|
| `src/estore-index.tsx` | Second Vite entry: `mount` / `unmount` for eStore React root |
| `src/estore/EStoreApp.tsx` | Root component, owns `page` state, renders layout + page |
| `src/estore/layout/EStoreLayout.tsx` | Sidebar (5 nav items + Help footer) + main content area |
| `src/estore/pages/BrandsPage.tsx` | Brands placeholder page |
| `src/estore/pages/CategoriesPage.tsx` | Categories placeholder page |
| `src/estore/pages/ProductsPage.tsx` | Products placeholder page |
| `src/estore/pages/AttributesPage.tsx` | Attributes placeholder page |
| `src/estore/pages/FulfillmentOptionsPage.tsx` | Fulfillment Options placeholder page |

### Modified files — Frontend

| File | Change |
|---|---|
| `vite.config.ts` | Switch from `lib` mode to `rollupOptions.input` with two entries; preserve existing output filenames |

### New files — Backend / App_Plugins

| File | Purpose |
|---|---|
| `App_Plugins/DcmsV16/dcms-estore-section.js` | Web component that loads `estore-spa.js` into Umbraco |

### Modified files — Backend / App_Plugins

| File | Change |
|---|---|
| `App_Plugins/DcmsV16/umbraco-package.json` | Add `dcmsEStore` section extension |
| `DcmsSectionAliases.cs` | Add `EStore = "dcmsEStore"` constant |
| `GrantDcmsCustomSectionsNotificationHandler.cs` | Add `DcmsSectionAliases.EStore` to the sections array |

---

## Sidebar Design

Visual style identical to Orders: `stone-50` background, `red-700` active accent with right border indicator.

| # | Label | Icon (SVG inline) |
|---|---|---|
| 1 | Brands | Shop/store icon |
| 2 | Categories | Grid/list icon |
| 3 | Products | Box/package icon |
| 4 | Attributes | Tag icon |
| 5 | Fulfillment Options | Truck/shipping icon |

Footer: Help Center button (same as Orders).

---

## Page Content

All five pages are **placeholder stubs** matching the Orders page header pattern:
- Breadcrumb nav (`eStore / <Page Name>`)
- `<h1>` title
- Subtitle description
- "Coming soon" body (or empty table scaffold)

No API calls, no real data — scaffolding only in this phase.

---

## Vite Config Change

Switch from `lib` mode (single entry) to explicit `rollupOptions.input` (two entries):

```ts
build: {
  outDir,
  emptyOutDir: false,
  rollupOptions: {
    input: {
      'orders-spa': 'src/index.tsx',
      'estore-spa': 'src/estore-index.tsx',
    },
    output: {
      entryFileNames: '[name].js',
      assetFileNames: (info) => info.name?.endsWith('.css') ? '[name].css' : '[name].[ext]',
      banner: '/* process shim */',
    },
  },
}
```

The existing `orders-spa.js` / `orders-spa.css` filenames are preserved so no backend references break.

---

## Section Registration (umbraco-package.json addition)

```json
{
  "type": "section",
  "alias": "dcmsEStore",
  "name": "eStore",
  "weight": 940,
  "element": "/App_Plugins/DcmsV16/dcms-estore-section.js",
  "meta": {
    "label": "eStore",
    "pathname": "dcms-estore"
  }
}
```

---

## Out of Scope

- Real API integration for any eStore page (Phase 2+)
- RBAC per-page permission enforcement (handled by Umbraco section grant)
- eStore-specific icons file (reuses `orders/icons.tsx` shared icons)
