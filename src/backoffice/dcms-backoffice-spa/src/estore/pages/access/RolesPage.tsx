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
import { deleteRole, fetchRoles } from "../../api/rolesApi";
import type { RoleRow } from "../../api/rolesApi";

export type { RoleRow };

type RolesPageProps = {
  onAddRole?: () => void;
  onEditRole?: (roleAlias: string) => void;
  onManageModules?: (row: RoleRow) => void;
  authToken?: string;
};

export function RolesPage({ onAddRole, onEditRole, onManageModules, authToken }: RolesPageProps) {
  const [rows, setRows] = useState<RoleRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<RoleRow | null>(null);
  const [deleteSubmitting, setDeleteSubmitting] = useState(false);
  const [toast, setToast] = useState<{ message: string; visible: boolean }>({ message: "", visible: false });

  const loadRoles = useCallback(() => {
    setLoading(true);
    setError(null);
    fetchRoles(authToken)
      .then(setRows)
      .catch((e: unknown) => {
        setRows([]);
        setError(e instanceof Error ? e.message : "Failed to load roles");
      })
      .finally(() => setLoading(false));
  }, [authToken]);

  useEffect(() => {
    loadRoles();
  }, [loadRoles]);

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

  async function handleDeleteConfirm() {
    if (!deleteTarget) return;
    setDeleteSubmitting(true);
    try {
      await deleteRole(deleteTarget.alias, authToken);
      setToast({ message: `Role "${deleteTarget.name}" deleted.`, visible: true });
      setDeleteTarget(null);
      loadRoles();
    } catch (e: unknown) {
      setToast({
        message: e instanceof Error ? e.message : "Delete failed",
        visible: true,
      });
    } finally {
      setDeleteSubmitting(false);
    }
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
              onClick={() => onManageModules?.(row.original)}
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
    <div className="-m-6 relative flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Roles management">
      {loading && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-surface/60 backdrop-blur-sm">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
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
          {error && (
            <p className="rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-xs font-medium text-error" role="alert">
              {error}
            </p>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant disabled:opacity-50"
            onClick={exportToCSV}
            disabled={rows.length === 0}
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
        <DataTable columns={columns} data={rows} globalFilterPlaceholder="Search by role name…" emptyMessage="No roles found." itemNoun="roles" />
      </div>

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
                  Remove <span className="font-semibold text-on-surface">{deleteTarget.name}</span> (
                  <span className="font-mono">{deleteTarget.alias}</span>)? You cannot delete a role that still has
                  members.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTarget(null)}
                disabled={deleteSubmitting}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity disabled:opacity-50"
                onClick={() => void handleDeleteConfirm()}
                disabled={deleteSubmitting}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

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
