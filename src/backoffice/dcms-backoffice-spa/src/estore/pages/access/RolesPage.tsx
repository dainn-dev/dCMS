import { useCallback, useEffect, useMemo, useState } from "react";
import { DataTable } from "../../../orders/components/DataTable";
import type { ColumnDef } from "@tanstack/react-table";
import {
  IconAddCircle,
  IconAdminPanel,
  IconCheckCircle,
  IconDelete,
  IconEdit,
  IconShare,
  IconWarning,
} from "../../../orders/icons";

// TODO: Replace with real API call to GET /umbraco/dcms/api/access/roles

export type RoleRow = {
  alias: string;
  name: string;
  description: string;
  isTenantRole: boolean;
  memberCount: number;
};

const DEMO_ROLES: RoleRow[] = [
  { alias: "it-admin", name: "IT Administrator", description: "Full system access including infrastructure and deployment.", isTenantRole: false, memberCount: 2 },
  { alias: "sys-admin", name: "System Administrator", description: "Platform-wide configuration and user management.", isTenantRole: false, memberCount: 3 },
  { alias: "ecom-mgr", name: "Ecommerce Manager", description: "Manages products, campaigns and promotions across all brands.", isTenantRole: false, memberCount: 5 },
  { alias: "tenant-pm", name: "Tenant Product Manager", description: "Product catalogue management scoped to assigned tenants.", isTenantRole: true, memberCount: 12 },
  { alias: "tenant-inv-mgr", name: "Tenant Inventory Manager", description: "Inventory and stock management for tenant-owned products.", isTenantRole: true, memberCount: 8 },
  { alias: "operations", name: "Operations", description: "Order fulfilment, logistics and dispatch operations.", isTenantRole: false, memberCount: 7 },
  { alias: "finance", name: "Finance", description: "Financial reporting, invoicing and payment reconciliation.", isTenantRole: false, memberCount: 4 },
  { alias: "brand-mgr", name: "Brand Manager", description: "Brand profile management and marketing content publishing.", isTenantRole: true, memberCount: 15 },
  { alias: "product-upload", name: "Product Upload", description: "Bulk product data import and catalogue upload operations.", isTenantRole: true, memberCount: 10 },
  { alias: "guest", name: "Guest", description: "Read-only access for external reviewers and auditors.", isTenantRole: false, memberCount: 3 },
];

type RolesPageProps = {
  onAddRole?: () => void;
  onEditRole?: (roleAlias: string) => void;
  onManageModules?: (roleAlias: string) => void;
};

export function RolesPage({ onAddRole, onEditRole, onManageModules }: RolesPageProps) {
  const [rows, setRows] = useState<RoleRow[]>(DEMO_ROLES);
  const [deleteTarget, setDeleteTarget] = useState<RoleRow | null>(null);
  const [toast, setToast] = useState<{ message: string; visible: boolean }>({ message: "", visible: false });

  useEffect(() => {
    if (!toast.visible) return;
    const t = setTimeout(() => setToast((p) => ({ ...p, visible: false })), 3000);
    return () => clearTimeout(t);
  }, [toast.visible]);

  const requestDelete = useCallback(
    (alias: string) => {
      const row = rows.find((r) => r.alias === alias);
      if (row) setDeleteTarget(row);
    },
    [rows],
  );

  function handleDeleteConfirm() {
    if (!deleteTarget) return;
    setRows((prev) => prev.filter((r) => r.alias !== deleteTarget.alias));
    setToast({ message: `Role "${deleteTarget.name}" removed.`, visible: true });
    setDeleteTarget(null);
  }

  function exportToCSV() {
    const headers = ["Alias", "Name", "Description", "Tenant Role", "Members"];
    const csvRows = rows.map((r) =>
      [r.alias, r.name, r.description, r.isTenantRole ? "Yes" : "No", String(r.memberCount)]
        .map((v) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v))
        .join(","),
    );
    const content = "\uFEFF" + [headers.join(","), ...csvRows].join("\n");
    const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "roles-export.csv";
    a.click();
    URL.revokeObjectURL(url);
    setToast({ message: "Roles exported to CSV.", visible: true });
  }

  const columns = useMemo<ColumnDef<RoleRow>[]>(
    () => [
      {
        accessorKey: "name",
        header: "Name",
        cell: ({ row }) => (
          <span className="text-xs font-semibold text-on-surface">{row.original.name}</span>
        ),
      },
      {
        accessorKey: "description",
        header: "Description",
        cell: ({ row }) => (
          <span className="text-xs text-on-surface-variant">{row.original.description}</span>
        ),
      },
      {
        accessorKey: "isTenantRole",
        header: "Tenant Role",
        cell: ({ row }) =>
          row.original.isTenantRole ? (
            <span className="inline-flex items-center rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary">
              Yes
            </span>
          ) : (
            <span className="inline-flex items-center rounded-full bg-surface-container-high px-2.5 py-0.5 text-[10px] font-bold uppercase text-on-surface-variant/60">
              No
            </span>
          ),
      },
      {
        accessorKey: "memberCount",
        header: "Members",
        cell: ({ row }) => (
          <span className="font-mono text-xs font-bold text-on-surface">{row.original.memberCount}</span>
        ),
      },
      {
        id: "actions",
        header: () => <span className="sr-only">Actions</span>,
        cell: ({ row }) => (
          <div className="flex items-center justify-end gap-1">
            <button
              type="button"
              title="Edit role"
              className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
              onClick={() => onEditRole?.(row.original.alias)}
            >
              <IconEdit className="h-4 w-4" />
            </button>
            <button
              type="button"
              title="Manage modules"
              className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
              onClick={() => onManageModules?.(row.original.alias)}
            >
              <IconAdminPanel className="h-4 w-4" />
            </button>
            <button
              type="button"
              title="Delete role"
              className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
              onClick={() => requestDelete(row.original.alias)}
            >
              <IconDelete className="h-4 w-4" />
            </button>
          </div>
        ),
      },
    ],
    [onEditRole, onManageModules, requestDelete],
  );

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Roles management">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span>Access</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Roles</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Roles</h1>
          <p className="text-sm text-on-surface-variant">Manage user groups and access right configurations.</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
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
            onClick={() => onAddRole?.()}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Create New Role
          </button>
        </div>
      </header>

      <div className="flex-1 p-6">
        <DataTable columns={columns} data={rows} globalFilterPlaceholder="Search by role name…" />
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
                <h3 className="text-sm font-bold text-on-surface">Delete role</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Remove <span className="font-semibold text-on-surface">{deleteTarget.name}</span> from the system?
                  Users assigned to this role will lose their associated permissions.
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
