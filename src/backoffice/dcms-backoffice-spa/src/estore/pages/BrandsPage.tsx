import { useMemo } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconAddCircle, IconDelete, IconShare } from "../../orders/icons";
import { createBrandColumns } from "../brands-columns";
import type { BrandListRow } from "../brands-columns";

// Re-export so EStoreApp.tsx import stays unchanged
export type { BrandListRow } from "../brands-columns";

const BRAND_ROWS: BrandListRow[] = [
  {
    code: "CAS-7721",
    name: "Luxe Heritage Group",
    imageAlt: "Minimalist abstract geometric logo with sharp angles and clean lines on a neutral background",
    imageSrc: "https://lh3.googleusercontent.com/aida-public/AB6AXuCE3gnGOwxCBJd-O91kFWC9Ys7MmX3uUyKEP-Uue7NKGilCk0aTlec5YS0d89W8x9X8lkFFpgpHVGV3OTFE7z1Kf6xTIMgldQEaj23BDB0Mtbv9BM66FWGZxZ6pMOJmbzOcMIXlCDJHHewraamlXGLG10IQ4Dj0iVkie9tj7INZzIeLlaFZ7NAzTRb_bi7sWHuiRmYFDCePqwhSc4knDdag68Xf1rqs61T6Rcndy0QGtMQFBuMRLTgEX1fBqRnBJfJV1E1j8AvJKSA",
    active: true,
  },
  {
    code: "VEL-4490",
    name: "Velocity Tech Systems",
    imageAlt: "Modern corporate emblem with a circular motif and elegant serif typography in a dark muted tone",
    imageSrc: "https://lh3.googleusercontent.com/aida-public/AB6AXuBOW2Bfv61jFiGsVEhs7pzGpS2bL8mHHiCdt3wGBYJ_ood63gKxVLi90yWNmxSQ7mhrlQ3p9VmqGoBTixTSsBi9zOrCnT1n0TSew0T3VEsqKqtdiUPOkvLmI6-5nodDQxlZpHnpWLE6aa1tw7yCiISzAgIDdR22bNseDLp-UUPH7h465EwLX5K5cuYiIyZrUDNNsjZ6xe9s4Frg-I-4hjmFYXRHpU-42kVk5L0mwUp9xsrKgyqj0KTv29mjGsNBAKQDGn9F0vWs0i4",
    active: true,
  },
  {
    code: "NOM-1022",
    name: "Nomad Consulting Ltd.",
    imageAlt: "Monochrome architectural brand logo using bold slab-serif lettering for a professional consulting firm",
    imageSrc: "https://lh3.googleusercontent.com/aida-public/AB6AXuB7yIQm-Rz09NCKbfnAZSD_9byiIdPPRNMvM1zXkTvyIowg7YuBmlKlQnwxaS8E0aITXNzhLloRDUYZljsWGyHB7ExQysjzKkTNPOg7NHucWjXDqdtDDb658MXRzFMDnyOCI4yCJgMy7tOks2pnxS_yLLsL1-FHXBqE2YhkrZkb5ym2Nbv-Zj_QohTQ7PfY-dFy7p0DiUxzLm9-wRbWIq-ilG5Sh0amZH3_jZmD0KfT0LuHvXN05S7bOnalnXlWaroTbAAijJdKV_I",
    active: false,
  },
  {
    code: "AUR-5501",
    name: "Aura Essentials",
    imageAlt: "Stylized floral emblem logo in soft golden tones on a clean white field for a luxury skincare brand",
    imageSrc: "https://lh3.googleusercontent.com/aida-public/AB6AXuAMgUTMGnlA5lx3Ejy3wYKSGMHWnJv8lCV_jSKJ2sVYXFo94qLsU4VjhhZLcgBNfst7UdqOk45Ld4PSbA4U9mtdFPfI6iXkwbrSlXMkg_UdlONO9bGP6roflznlaLK3HdEAqeL9ef6KZmVW4uptu5pb_PNpTgkX_8JYm4Di2d2JaUHbOQ_SzXCCzLbeyb0I67i5nfwf1u2gNeJiSJk5IB0nweV7Xwm2hT-_bIw",
    active: true,
  },
];

type BrandsPageProps = {
  onEditBrand?: (row: BrandListRow) => void;
  onCreateBrand?: () => void;
};

export function BrandsPage({ onEditBrand, onCreateBrand }: BrandsPageProps) {
  const columns = useMemo(
    () => createBrandColumns(onEditBrand, (code) => console.info("[Brands] Delete", code)),
    [onEditBrand]
  );

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Brands management">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Brands</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Brands</h1>
          <p className="text-sm text-on-surface-variant">
            Manage and audit enterprise brand identities across all channels.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant"
            onClick={() => console.info("[Brands] Export (placeholder)")}
          >
            <IconShare className="h-4 w-4 shrink-0" />
            Export Brands
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-error/30 px-4 py-2 text-xs font-medium text-error transition-colors hover:bg-error/10"
            onClick={() => console.info("[Brands] Remove brand (placeholder)")}
          >
            <IconDelete className="h-4 w-4 shrink-0" />
            Remove Brand
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container"
            onClick={() => onCreateBrand?.()}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Create a New Brand
          </button>
        </div>
      </header>

      <div className="flex-1 p-6">
        <DataTable
          columns={columns}
          data={BRAND_ROWS}
          globalFilterPlaceholder="Search by code or brand name…"
        />
      </div>
    </div>
  );
}
