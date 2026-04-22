import { useCallback, useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconAddCircle, IconCheckCircle, IconDelete, IconShare, IconWarning } from "../../orders/icons";
import { createBrandColumns } from "../brands-columns";
import type { BrandListRow } from "../brands-columns";
import { exportBrandsToXlsx } from "../exportBrandsXlsx";

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
  rows?: BrandListRow[];
  onRowsChange?: (next: BrandListRow[]) => Promise<void> | void;
  loading?: boolean;
  loadError?: string | null;
};

export function BrandsPage({ onEditBrand, onCreateBrand, rows, onRowsChange, loading, loadError }: BrandsPageProps) {
  const tableRows = rows ?? BRAND_ROWS;

  const [deleteTarget, setDeleteTarget] = useState<BrandListRow | null>(null);

  const requestDelete = useCallback(
    (code: string) => {
      const row = tableRows.find((r) => r.code === code);
      if (row) setDeleteTarget(row);
    },
    [tableRows]
  );

  const columns = useMemo(
    () => createBrandColumns(onEditBrand, requestDelete),
    [onEditBrand, requestDelete]
  );

  function handleDeleteConfirm() {
    if (!deleteTarget) return;
    if (onRowsChange) {
      onRowsChange(tableRows.filter((r) => r.code !== deleteTarget.code));
      setToast({ message: "Brand removed.", visible: true });
    } else {
      setToast({ message: "Delete is not available in this view.", visible: true });
    }
    setDeleteTarget(null);
  }

  const [toast, setToast] = useState<{ message: string; visible: boolean }>({
    message: "",
    visible: false,
  });

  useEffect(() => {
    if (!toast.visible) return;
    const t = setTimeout(() => setToast((p) => ({ ...p, visible: false })), 3000);
    return () => clearTimeout(t);
  }, [toast.visible]);

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
            onClick={async () => {
              try {
                await exportBrandsToXlsx(tableRows);
                setToast({ message: "Brand export downloaded (.xlsx).", visible: true });
              } catch {
                setToast({ message: "Export failed. Try again.", visible: true });
              }
            }}
          >
            <IconShare className="h-4 w-4 shrink-0" />
            Export Brands
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
        {loading && (
          <div className="flex items-center justify-center py-16 text-on-surface-variant text-sm gap-3">
            <svg className="animate-spin h-5 w-5 text-primary" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
            </svg>
            Loading brands…
          </div>
        )}
        {!loading && loadError && (
          <div className="rounded-lg border border-error/30 bg-error-container/20 p-4 text-sm text-error">
            Failed to load brands: {loadError}
          </div>
        )}
        {!loading && !loadError && (
        <DataTable
          columns={columns}
          data={tableRows}
          globalFilterPlaceholder="Search by code or brand name…"
        />
        )}
      </div>

      {/* Delete confirmation */}
      {deleteTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete brand</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Remove <span className="font-semibold text-on-surface">{deleteTarget.name}</span> (
                  {deleteTarget.code}) from the list? This demo does not call a server; the row disappears from the
                  table.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTarget(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={handleDeleteConfirm}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toast notification */}
      <div
        aria-live="polite"
        className={`fixed bottom-6 left-1/2 z-50 -translate-x-1/2 transition-all duration-300 ${
          toast.visible ? "translate-y-0 opacity-100" : "translate-y-4 opacity-0 pointer-events-none"
        }`}
      >
        <div className="flex items-center gap-3 rounded-xl bg-on-surface px-5 py-3 shadow-2xl">
          <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
          <span className="text-sm font-medium text-surface">{toast.message}</span>
          <button
            type="button"
            aria-label="Dismiss"
            className="ml-2 rounded p-0.5 text-surface/60 hover:text-surface transition-colors"
            onClick={() => setToast((p) => ({ ...p, visible: false }))}
          >
            ✕
          </button>
        </div>
      </div>
    </div>
  );
}
