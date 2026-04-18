import { useCallback, useEffect, useMemo, useState } from "react";
import { DataTable } from "../../../orders/components/DataTable";
import type { ColumnDef } from "@tanstack/react-table";
import {
  IconAddCircle,
  IconCheckCircle,
  IconDelete,
  IconEdit,
  IconSearch,
  IconShare,
  IconWarning,
} from "../../../orders/icons";

// TODO: Replace with real API call to GET /umbraco/dcms/api/access/tenants

export type TenantRow = {
  id: string;
  code: string;
  name: string;
  contactName: string;
  contactEmail: string;
  brandCount: number;
  active: boolean;
};

const DEMO_TENANTS: TenantRow[] = [
  { id: "t-001", code: "GES", name: "Global Enterprise Solutions", contactName: "Marcus Chen", contactEmail: "marcus.chen@ges.com", brandCount: 4, active: true },
  { id: "t-002", code: "EMEA", name: "EMEA Retail Group", contactName: "Sophie Laurent", contactEmail: "sophie.laurent@emea-retail.com", brandCount: 6, active: true },
  { id: "t-003", code: "NABD", name: "North America Brand Div", contactName: "James Williams", contactEmail: "james.w@nabd.io", brandCount: 3, active: true },
  { id: "t-004", code: "APAC", name: "APAC Commerce Hub", contactName: "Yuki Tanaka", contactEmail: "yuki.tanaka@apac-hub.com", brandCount: 8, active: true },
  { id: "t-005", code: "LATAM", name: "LatAm Online Marketplace", contactName: "Carlos Mendez", contactEmail: "c.mendez@latam-market.co", brandCount: 5, active: false },
  { id: "t-006", code: "MEA", name: "Middle East & Africa Retail", contactName: "Amira Hassan", contactEmail: "amira.hassan@mea-retail.ae", brandCount: 2, active: true },
];

type TenantsPageProps = {
  onAddTenant?: () => void;
  onEditTenant?: (tenantId: string) => void;
};

export function TenantsPage({ onAddTenant, onEditTenant }: TenantsPageProps) {
  const [rows, setRows] = useState<TenantRow[]>(DEMO_TENANTS);
  const [search, setSearch] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<TenantRow | null>(null);
  const [toast, setToast] = useState<{ message: string; visible: boolean }>({ message: "", visible: false });

  useEffect(() => {
    if (!toast.visible) return;
    const t = setTimeout(() => setToast((p) => ({ ...p, visible: false })), 3000);
    return () => clearTimeout(t);
  }, [toast.visible]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) =>
        r.code.toLowerCase().includes(q) ||
        r.name.toLowerCase().includes(q) ||
        r.contactName.toLowerCase().includes(q) ||
        r.contactEmail.toLowerCase().includes(q),
    );
  }, [rows, search]);

  const requestDelete = useCallback(
    (id: string) => {
      const row = rows.find((r) => r.id === id);
      if (row) setDeleteTarget(row);
    },
    [rows],
  );

  function handleDeleteConfirm() {
    if (!deleteTarget) return;
    setRows((prev) => prev.filter((r) => r.id !== deleteTarget.id));
    setToast({ message: `Tenant "${deleteTarget.name}" removed.`, visible: true });
    setDeleteTarget(null);
  }

  function exportToCSV() {
    const headers = ["ID", "Code", "Name", "Contact Name", "Contact Email", "Brands", "Active"];
    const csvRows = filtered.map((r) =>
      [r.id, r.code, r.name, r.contactName, r.contactEmail, String(r.brandCount), r.active ? "Yes" : "No"]
        .map((v) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v))
        .join(","),
    );
    const content = "\uFEFF" + [headers.join(","), ...csvRows].join("\n");
    const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "tenants-export.csv";
    a.click();
    URL.revokeObjectURL(url);
    setToast({ message: "Tenants exported to CSV.", visible: true });
  }

  const columns = useMemo<ColumnDef<TenantRow>[]>(
    () => [
      {
        accessorKey: "code",
        header: "Code",
        cell: ({ row }) => (
          <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">
            {row.original.code}
          </code>
        ),
      },
      {
        accessorKey: "name",
        header: "Name",
        cell: ({ row }) => (
          <div className="flex items-center gap-2">
            <span className={`h-2 w-2 rounded-full shrink-0 ${row.original.active ? "bg-green-500" : "bg-outline-variant/40"}`} />
            <span className="text-xs font-semibold text-on-surface">{row.original.name}</span>
          </div>
        ),
      },
      {
        accessorKey: "contactName",
        header: "Contact Name",
        cell: ({ row }) => <span className="text-xs text-on-surface">{row.original.contactName}</span>,
      },
      {
        accessorKey: "contactEmail",
        header: "Contact Email",
        cell: ({ row }) => (
          <span className="text-xs text-on-surface-variant">{row.original.contactEmail}</span>
        ),
      },
      {
        accessorKey: "brandCount",
        header: "Brands",
        cell: ({ row }) => (
          <span className="inline-flex items-center rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold text-primary">
            {row.original.brandCount}
          </span>
        ),
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        cell: ({ row }) => (
          <div className="flex items-center justify-end gap-1">
            <button
              type="button"
              title="Edit tenant"
              className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
              onClick={() => onEditTenant?.(row.original.id)}
            >
              <IconEdit className="h-4 w-4" />
            </button>
            <button
              type="button"
              title="Delete tenant"
              className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
              onClick={() => requestDelete(row.original.id)}
            >
              <IconDelete className="h-4 w-4" />
            </button>
          </div>
        ),
      },
    ],
    [onEditTenant, requestDelete],
  );

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Tenants management">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span>Access</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Tenants</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Tenants</h1>
          <p className="text-sm text-on-surface-variant">Manage platform tenants (Siêu thị) and their brand portfolios.</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2 rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-3 py-2">
            <IconSearch className="h-4 w-4 shrink-0 text-on-surface-variant" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by code, name or contact…"
              className="w-56 bg-transparent text-xs outline-none placeholder:text-on-surface-variant/50"
            />
          </div>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant"
            onClick={exportToCSV}
          >
            <IconShare className="h-4 w-4 shrink-0" />
            Export
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container"
            onClick={() => onAddTenant?.()}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Add New Tenant
          </button>
        </div>
      </header>

      <div className="flex-1 p-6">
        <DataTable columns={columns} data={filtered} globalFilterPlaceholder="Search tenants…" />
      </div>

      {/* Delete confirmation modal */}
      {deleteTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete tenant</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Remove <span className="font-semibold text-on-surface">{deleteTarget.name}</span> (
                  {deleteTarget.code}) from the platform? All associated brands and stores will be affected.
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

      {/* Toast */}
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
