import { useCallback, useEffect, useMemo, useState } from "react";
import { DataTable } from "../../../orders/components/DataTable";
import type { ColumnDef } from "@tanstack/react-table";
import {
  IconAddCircle,
  IconCheckCircle,
  IconDelete,
  IconEdit,
  IconKey,
  IconSearch,
  IconShare,
  IconWarning,
} from "../../../orders/icons";

// TODO: Replace with real API call to GET /umbraco/dcms/api/access/users
export type UserRow = {
  id: string;
  username: string;
  name: string;
  email: string;
  role: string;
  active: boolean;
};

const DEMO_USERS: UserRow[] = [
  { id: "u-001", username: "superadmin", name: "Super Admin", email: "superadmin@dcms.io", role: "IT Administrator", active: true },
  { id: "u-002", username: "sys.admin", name: "System Administrator", email: "sysadmin@dcms.io", role: "System Administrator", active: true },
  { id: "u-003", username: "ecom.mgr", name: "Ecommerce Manager", email: "ecom.mgr@dcms.io", role: "Ecommerce Manager", active: true },
  { id: "u-004", username: "brand.mgr1", name: "Brand Manager One", email: "brand.mgr1@dcms.io", role: "Brand Manager", active: true },
  { id: "u-005", username: "product.upload1", name: "Product Upload User", email: "product.upload1@dcms.io", role: "Product Upload", active: true },
  { id: "u-006", username: "finance.user", name: "Finance User", email: "finance@dcms.io", role: "Finance", active: false },
  { id: "u-007", username: "ops.user1", name: "Operations User", email: "ops1@dcms.io", role: "Operations", active: true },
  { id: "u-008", username: "tenant.pm1", name: "Tenant Product Manager", email: "tenant.pm1@dcms.io", role: "Tenant Product Manager", active: true },
  { id: "u-009", username: "guest.user", name: "Guest User", email: "guest@dcms.io", role: "Guest", active: false },
  { id: "u-010", username: "inv.mgr1", name: "Inventory Manager", email: "inv.mgr1@dcms.io", role: "Tenant Inventory Manager", active: true },
];

type UsersPageProps = {
  onAddUser?: () => void;
  onEditUser?: (userId: string) => void;
  onChangePassword?: (userId: string) => void;
};

export function UsersPage({ onAddUser, onEditUser, onChangePassword }: UsersPageProps) {
  const [rows, setRows] = useState<UserRow[]>(DEMO_USERS);
  const [search, setSearch] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<UserRow | null>(null);
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
        r.username.toLowerCase().includes(q) ||
        r.name.toLowerCase().includes(q) ||
        r.email.toLowerCase().includes(q),
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
    setToast({ message: `User "${deleteTarget.username}" removed.`, visible: true });
    setDeleteTarget(null);
  }

  function exportToCSV() {
    const headers = ["ID", "Username", "Name", "Email", "Role", "Active"];
    const csvRows = filtered.map((r) =>
      [r.id, r.username, r.name, r.email, r.role, r.active ? "Yes" : "No"]
        .map((v) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v))
        .join(","),
    );
    const content = "\uFEFF" + [headers.join(","), ...csvRows].join("\n");
    const blob = new Blob([content], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "users-export.csv";
    a.click();
    URL.revokeObjectURL(url);
    setToast({ message: "Users exported to CSV.", visible: true });
  }

  const columns = useMemo<ColumnDef<UserRow>[]>(
    () => [
      {
        accessorKey: "username",
        header: "Username",
        cell: ({ row }) => (
          <span className="font-mono text-xs font-semibold text-on-surface">{row.original.username}</span>
        ),
      },
      {
        accessorKey: "name",
        header: "Name",
        cell: ({ row }) => <span className="text-xs text-on-surface">{row.original.name}</span>,
      },
      {
        accessorKey: "email",
        header: "Email",
        cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.original.email}</span>,
      },
      {
        accessorKey: "role",
        header: "Role",
        cell: ({ row }) => (
          <span className="inline-flex items-center rounded-full bg-surface-container-high px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-on-surface-variant">
            {row.original.role}
          </span>
        ),
      },
      {
        accessorKey: "active",
        header: "Active",
        cell: ({ row }) =>
          row.original.active ? (
            <span className="inline-flex items-center rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary">
              Active
            </span>
          ) : (
            <span className="inline-flex items-center rounded-full bg-surface-container-high px-2.5 py-0.5 text-[10px] font-bold uppercase text-on-surface-variant/60">
              Inactive
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
              title="Edit user"
              className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
              onClick={() => onEditUser?.(row.original.id)}
            >
              <IconEdit className="h-4 w-4" />
            </button>
            <button
              type="button"
              title="Change password"
              className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
              onClick={() => onChangePassword?.(row.original.id)}
            >
              <IconKey className="h-4 w-4" />
            </button>
            <button
              type="button"
              title="Delete user"
              className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
              onClick={() => requestDelete(row.original.id)}
            >
              <IconDelete className="h-4 w-4" />
            </button>
          </div>
        ),
      },
    ],
    [onEditUser, onChangePassword, requestDelete],
  );

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Users management">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span>Access</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Users</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Users</h1>
          <p className="text-sm text-on-surface-variant">Manage backoffice user accounts and role assignments.</p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2 rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-3 py-2">
            <IconSearch className="h-4 w-4 shrink-0 text-on-surface-variant" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by username, name or email…"
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
            onClick={() => onAddUser?.()}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Add New User
          </button>
        </div>
      </header>

      <div className="flex-1 p-6">
        <DataTable columns={columns} data={filtered} globalFilterPlaceholder="Search users…" />
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
                <h3 className="text-sm font-bold text-on-surface">Delete user</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Remove <span className="font-semibold text-on-surface">{deleteTarget.name}</span> (
                  {deleteTarget.username}) from the system? This action cannot be undone.
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
