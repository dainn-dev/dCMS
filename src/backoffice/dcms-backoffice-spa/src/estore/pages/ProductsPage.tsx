import {
  IconAddCircle,
  IconChevronRight,
  IconCloudUpload,
  IconDownload,
  IconEdit,
  IconFirstPage,
  IconLastPage,
  IconChevronLeft,
  IconChevronRight,
  IconLayers,
  IconOpenInNew,
  IconVisibility,
} from "../../orders/icons";

const labelFilter = "text-[10px] font-bold text-on-surface-variant uppercase tracking-wider";
const inputFilter =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:ring-1 focus:ring-primary outline-none";

export type ProductListRow = {
  id: string;
  name: string;
  categoryPath: string;
  brand: string;
  upc: string;
  sku: string;
  price: string;
  qty: number;
  eStoreLabel: string;
  eStoreVariant: "live" | "low-stock" | "offline";
  statusLabel: string;
  statusVariant: "active" | "out-of-stock";
  modified: string;
  imageSrc: string;
  imageAlt: string;
};

const PRODUCT_ROWS: ProductListRow[] = [
  {
    id: "1",
    name: "Vantage Series 5 Watch",
    categoryPath: "Timepieces > Luxury",
    brand: "Cronos Ltd.",
    upc: "400234110",
    sku: "WT-550-B",
    price: "$549.00",
    qty: 124,
    eStoreLabel: "Live",
    eStoreVariant: "live",
    statusLabel: "Active",
    statusVariant: "active",
    modified: "Oct 12, 2023",
    imageAlt: "minimalist modern watch product shot on clean white background with soft shadows",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuBQGVuwgea0Cqfj1gQeahtKlyScXePS2WTXEXlaLjBqC48b60oWv_8VjCZXUHPBgddtXv5yolcdoNx686Mqq7V5t1T6ELmgfGBDzH0OsLHdlcAZG5sbskFzOBUtqgONEM_8afgtdGTXFpIP9mcDI9Xvp1IU6Dq47meMpWiWSpSO_IuhhvwDnwa5NVXdtQefSGKJ0M2tobZ5pUj2X0K6fzIr32JmWIt2V-P63NkJOOQdvmK0P1CTdIuptZcNO9ujs79hOj8CtJ4HmJc",
  },
  {
    id: "2",
    name: "Echo-Noise Headphones",
    categoryPath: "Audio > Wireless",
    brand: "Sonic Bloom",
    upc: "400234111",
    sku: "AU-102-S",
    price: "$299.00",
    qty: 12,
    eStoreLabel: "Low Stock",
    eStoreVariant: "low-stock",
    statusLabel: "Active",
    statusVariant: "active",
    modified: "Oct 11, 2023",
    imageAlt: "professional studio shot of noise-cancelling headphones on a warm wood surface",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuABsa3VLstm8xkOM8pTEmpECWRGPbZ6Wu1nACeFLb-UHb5kI0vaMaF17BQGOfvBcUvh6twmyaOFL4k7ZyJlVCYowRmZ_zQJsFVa9T_FV2rLDlL8gOkEs6IsUAV0f9W_ji4cy5ejl2_RD69de3XVqqEAYzbUfzY9AbyAqkaiXSV47AZSF6SAxedG5RiwrIu9T6VDZGZHe-ygSdKSp6xDQfVN2RevDUK_QSzhbQVDCj-REFTo5s-Hs_L6fyhdqYVVvatZs0X7P9KhEyI",
  },
  {
    id: "3",
    name: "SwiftRun Pro Z",
    categoryPath: "Footwear > Athletics",
    brand: "Velocity Sport",
    upc: "400234115",
    sku: "FT-99-R",
    price: "$120.00",
    qty: 0,
    eStoreLabel: "Offline",
    eStoreVariant: "offline",
    statusLabel: "Out of Stock",
    statusVariant: "out-of-stock",
    modified: "Oct 09, 2023",
    imageAlt: "vibrant red sneaker on a clean minimalist studio background with sharp lighting",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuC3s1mKURMJ9Ev9YS9JjPeP5VsFm_t4eldi7XmGVtSvenw7tMsr48em8tjJ4VwiA4RdwAh0cmRezEnQc9MGmUHRC47g1CKAMopomHys9OEQtbFvrsl3B8wM5QmCPtzJYe-f-YCIDd-wXvSRYfQRN-eXJczwdtPVh7ya7WGpiHCekm7A9ll02Nvh97T6k4-3n2eMEshY8kl1eKZDi9edqm9fUrUralRFrRcLYz4fNO2bUsu_B8zpfKPVfDXbA8vt6-rcD4-fVips-v4",
  },
  {
    id: "4",
    name: "InstaCam Retro X",
    categoryPath: "Photography > Gadgets",
    brand: "Optic Visions",
    upc: "400234120",
    sku: "CM-42-P",
    price: "$185.00",
    qty: 45,
    eStoreLabel: "Live",
    eStoreVariant: "live",
    statusLabel: "Active",
    statusVariant: "active",
    modified: "Oct 05, 2023",
    imageAlt: "vintage style polaroid camera on a minimalist grey background with professional lighting",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuDofZzQKDYKYwUX8w-CtCegEiQpnaYSBSDQntdqsTS3LkeMRWZmnv8y77KBS0tcjHsT49HG93aWBlUuj1dkKRckoiXdBXAxmGEWo4dwIhzh8o26B4yl9G8yOeqMQMl7q0fcEfR_zp0Abz6ZpQCM0Cy0JMSt5Fs9vMWvDrnBEHhna3e6tYr90r9490RT2n8d6qmMFsQwE0vwQxpfnfsdkhfAlfrAcLZHu_h2w72WCeR_2ymyfgqqeeKYaXmOWyVsXEjlw8cLIf0j_UM",
  },
];

function eStoreBadgeClasses(variant: ProductListRow["eStoreVariant"]) {
  switch (variant) {
    case "live":
      return "bg-secondary-container/20 text-on-secondary-container";
    case "low-stock":
      return "bg-error-container/40 text-on-error-container";
    case "offline":
      return "bg-on-surface-variant/10 text-on-surface-variant opacity-50";
    default:
      return "bg-outline-variant/20 text-on-surface-variant";
  }
}

function statusBadgeClasses(variant: ProductListRow["statusVariant"]) {
  switch (variant) {
    case "active":
      return "bg-tertiary-container/20 text-on-tertiary-fixed-variant";
    case "out-of-stock":
      return "bg-error-container text-on-error-container";
    default:
      return "bg-outline-variant/20 text-on-surface-variant";
  }
}

export function ProductsPage() {
  return (
    <div
      className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low"
      aria-label="Product manager"
    >
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <nav className="mb-2 flex items-center gap-2 text-[10px] font-medium uppercase tracking-widest text-on-surface-variant">
            <span>Estore</span>
            <IconChevronRight className="h-3 w-3 shrink-0 opacity-70" aria-hidden />
            <span className="font-bold text-primary">Products</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Product Manager</h1>
          <p className="mt-2 text-sm text-on-surface-variant">
            Search, filter, and bulk-manage catalog products across brands and categories.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="flex items-center gap-2 rounded bg-surface-container-low px-4 py-2 text-xs font-semibold text-on-surface transition-all hover:bg-surface-container-high"
            onClick={() => console.info("[Products] Import (placeholder)")}
          >
            <IconCloudUpload className="h-4 w-4 shrink-0" />
            Import
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded bg-surface-container-low px-4 py-2 text-xs font-semibold text-on-surface transition-all hover:bg-surface-container-high"
            onClick={() => console.info("[Products] Export (placeholder)")}
          >
            <IconDownload className="h-4 w-4 shrink-0" />
            Export
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded bg-surface-container-low px-4 py-2 text-xs font-semibold text-on-surface transition-all hover:bg-surface-container-high"
            onClick={() => console.info("[Products] Group actions (placeholder)")}
          >
            <IconLayers className="h-4 w-4 shrink-0" />
            Group Actions
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded bg-primary px-6 py-2 text-xs font-bold text-on-primary shadow-sm transition-all hover:opacity-95"
            onClick={() => console.info("[Products] Add product (placeholder)")}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Add Product
          </button>
        </div>
      </header>

      <div className="flex-1 space-y-8 p-6 pb-24">
      <section className="rounded-xl bg-surface-container-low p-6">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5">
          <div className="space-y-1.5">
            <label className={labelFilter}>UPC</label>
            <input className={inputFilter} placeholder="Enter UPC" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>SKU</label>
            <input className={inputFilter} placeholder="Enter SKU" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Product Name</label>
            <input className={inputFilter} placeholder="Search by name" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Price (Min/Max)</label>
            <div className="flex gap-2">
              <input className={`${inputFilter} w-1/2`} placeholder="Min" type="number" />
              <input className={`${inputFilter} w-1/2`} placeholder="Max" type="number" />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Brand</label>
            <select className={`${inputFilter} appearance-none`}>
              <option>All Brands</option>
              <option>Premium Collection</option>
              <option>Eco-Essentials</option>
              <option>Luxe Goods</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Category</label>
            <select className={`${inputFilter} appearance-none`}>
              <option>All Categories</option>
              <option>Electronics</option>
              <option>Home &amp; Living</option>
              <option>Apparel</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>eStore Qty</label>
            <div className="flex gap-2">
              <input className={`${inputFilter} w-1/2`} placeholder="Min" type="number" />
              <input className={`${inputFilter} w-1/2`} placeholder="Max" type="number" />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Quick Access</label>
            <select className={inputFilter}>
              <option>Last 30 Days</option>
              <option>Out of Stock</option>
              <option>New Arrivals</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>eStore Status</label>
            <select className={inputFilter}>
              <option>All States</option>
              <option>Live</option>
              <option>Draft</option>
              <option>Inactive</option>
            </select>
          </div>
          <div className="flex items-end gap-2 lg:col-span-1">
            <button
              type="button"
              className="h-9 flex-1 rounded-lg bg-primary py-2 text-xs font-bold text-on-primary transition-all hover:opacity-90"
              onClick={() => console.info("[Products] Search (placeholder)")}
            >
              Search
            </button>
            <button
              type="button"
              className="h-9 rounded-lg bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant transition-all hover:bg-outline-variant/40"
              onClick={() => console.info("[Products] Reset filters (placeholder)")}
            >
              Reset
            </button>
          </div>
        </div>
      </section>

      <div className="overflow-hidden rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-[0_4px_20px_rgba(40,23,22,0.02)]">
        <div className="overflow-x-auto custom-scrollbar">
          <table className="w-full border-collapse text-left">
            <thead>
              <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                <th className="w-10 px-6 py-4 text-center">
                  <input
                    type="checkbox"
                    className="rounded-sm border-outline text-primary focus:ring-primary"
                    aria-label="Select all"
                  />
                </th>
                <th className="w-20 px-4 py-4">Image</th>
                <th className="px-4 py-4">Product Name</th>
                <th className="px-4 py-4">Brand</th>
                <th className="px-4 py-4">UPC / SKU</th>
                <th className="px-4 py-4 text-right">Price</th>
                <th className="px-4 py-4 text-center">Qty</th>
                <th className="px-4 py-4">eStore</th>
                <th className="px-4 py-4">Status</th>
                <th className="px-4 py-4">Modified</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-outline-variant/10">
              {PRODUCT_ROWS.map((row) => (
                <tr key={row.id} className="text-[12px] transition-colors hover:bg-surface-container-low">
                  <td className="px-6 py-4 text-center">
                    <input
                      type="checkbox"
                      className="rounded-sm border-outline text-primary focus:ring-primary"
                      aria-label={`Select ${row.name}`}
                    />
                  </td>
                  <td className="px-4 py-4">
                    <div className="h-10 w-10 overflow-hidden rounded border border-outline-variant/10 bg-surface-container-high">
                      <img className="h-full w-full object-cover" alt={row.imageAlt} src={row.imageSrc} />
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <p className="font-bold text-on-surface">{row.name}</p>
                    <span className="text-[10px] text-on-surface-variant">{row.categoryPath}</span>
                  </td>
                  <td className="px-4 py-4 font-medium text-on-surface-variant">{row.brand}</td>
                  <td className="px-4 py-4 font-mono text-[10px]">
                    <span className="block">UPC: {row.upc}</span>
                    <span className="block text-primary/60">SKU: {row.sku}</span>
                  </td>
                  <td className="px-4 py-4 text-right font-bold text-on-surface">{row.price}</td>
                  <td className="px-4 py-4 text-center font-medium">{row.qty}</td>
                  <td className="px-4 py-4">
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${eStoreBadgeClasses(row.eStoreVariant)}`}
                    >
                      {row.eStoreLabel}
                    </span>
                  </td>
                  <td className="px-4 py-4">
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${statusBadgeClasses(row.statusVariant)}`}
                    >
                      {row.statusLabel}
                    </span>
                  </td>
                  <td className="px-4 py-4 text-on-surface-variant">{row.modified}</td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex justify-end gap-2 text-on-surface-variant">
                      <button
                        type="button"
                        className="transition-colors hover:text-primary"
                        aria-label="Edit"
                        onClick={() => console.info("[Products] Edit", row.id)}
                      >
                        <IconEdit className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        className="transition-colors hover:text-primary"
                        aria-label="View"
                        onClick={() => console.info("[Products] View", row.id)}
                      >
                        <IconVisibility className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        className="transition-colors hover:text-primary"
                        aria-label="Open in new"
                        onClick={() => console.info("[Products] Open", row.id)}
                      >
                        <IconOpenInNew className="h-5 w-5" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <footer className="flex flex-col items-center justify-between gap-4 bg-surface-container px-6 py-4 sm:flex-row">
          <div className="flex flex-wrap items-center gap-4 text-[11px] font-medium text-on-surface-variant">
            <span>Showing 1 to 10 of 582 entries</span>
            <div className="flex items-center gap-2">
              <label htmlFor="products-page-size">Show</label>
              <select
                id="products-page-size"
                className="rounded border-none bg-surface-container-lowest px-2 py-1 text-[11px] focus:ring-1 focus:ring-primary focus:outline-none"
              >
                <option>10</option>
                <option>25</option>
                <option>50</option>
              </select>
            </div>
          </div>
          <div className="flex items-center gap-1">
            <button
              type="button"
              className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
              disabled
              aria-label="First page"
            >
              <IconFirstPage className="h-5 w-5" />
            </button>
            <button
              type="button"
              className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
              disabled
              aria-label="Previous page"
            >
              <IconChevronLeft className="h-5 w-5" />
            </button>
            <div className="mx-2 flex items-center gap-1">
              <button
                type="button"
                className="h-8 w-8 rounded bg-primary text-[11px] font-bold text-on-primary"
              >
                1
              </button>
              <button
                type="button"
                className="h-8 w-8 rounded text-[11px] font-medium text-on-surface hover:bg-surface-container-high"
              >
                2
              </button>
              <button
                type="button"
                className="h-8 w-8 rounded text-[11px] font-medium text-on-surface hover:bg-surface-container-high"
              >
                3
              </button>
              <span className="px-1">...</span>
              <button
                type="button"
                className="h-8 w-8 rounded text-[11px] font-medium text-on-surface hover:bg-surface-container-high"
              >
                59
              </button>
            </div>
            <button
              type="button"
              className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high"
              aria-label="Next page"
            >
              <IconChevronRight className="h-5 w-5" />
            </button>
            <button
              type="button"
              className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high"
              aria-label="Last page"
            >
              <IconLastPage className="h-5 w-5" />
            </button>
          </div>
        </footer>
      </div>

      </div>

      <div className="pointer-events-none fixed bottom-6 right-8 z-10 flex items-center gap-4 rounded-full border border-outline-variant/30 bg-surface/80 px-6 py-2 shadow-xl backdrop-blur-sm">
        <div className="pointer-events-auto flex items-center gap-2">
          <span className="h-2 w-2 animate-pulse rounded-full bg-primary" aria-hidden />
          <span className="text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            Live Sync Active
          </span>
        </div>
        <div className="h-4 w-px bg-outline-variant/30" aria-hidden />
        <p className="pointer-events-auto text-[10px] font-medium text-on-surface-variant">
          Last updated: 2 mins ago
        </p>
      </div>
    </div>
  );
}
