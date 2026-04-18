import { useMemo, useState } from "react";
import {
  IconAccountTree,
  IconAddCircle,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconDelete,
  IconDownload,
  IconEdit,
  IconInfo,
  IconTune,
} from "../../orders/icons";
import type {
  FulfillmentGrouping,
  FulfillmentSlot,
  StockLocation,
  FulfillmentDeliveryMode,
  FulfillmentDynamicField,
  FulfillmentFieldType,
  FulfillmentFieldSection,
  FulfillmentPredefinedFieldSetting,
} from "../EStoreApp";

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";

const FIELD_TYPES: FulfillmentFieldType[] = [
  "Text Box",
  "WYSIWYG",
  "Geo Location",
  "Dropdown List",
  "Checkbox",
  "Date Picker",
  "Multiple Select",
];

const FIELD_SECTIONS: FulfillmentFieldSection[] = [
  "Additional Info",
  "General Information",
  "Qualifier",
  "Fulfillment Settings",
  "Advanced Settings",
];

type Props = {
  groupings: FulfillmentGrouping[];
  slots: FulfillmentSlot[];
  stockLocations: StockLocation[];
  dynamicFields: FulfillmentDynamicField[];
  onDynamicFieldsChange: (next: FulfillmentDynamicField[]) => void;
  predefinedFieldSettings: FulfillmentPredefinedFieldSetting[];
  onPredefinedFieldSettingsChange: (next: FulfillmentPredefinedFieldSetting[]) => void;
  onOpenLogisticPartners: () => void;
  onCreateGrouping: (next: Omit<FulfillmentGrouping, "id">) => string;
  onUpdateGrouping: (id: string, next: Omit<FulfillmentGrouping, "id">) => void;
  onDeleteGrouping: (id: string) => void;
  onViewDeliverySlots: (groupingId: string) => void;
  onCreateTimeSlot: (groupingId: string) => void;
};

const DELIVERY_MODES: FulfillmentDeliveryMode[] = ["Store Collection", "Local Delivery", "Overseas Delivery"];

function seedCodeFromName(name: string) {
  const cleaned = name
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
  return cleaned || "FULFILLMENT_GROUP";
}

function csvEscape(val: string): string {
  if (/[",\n]/.test(val)) return `"${val.replace(/"/g, '""')}"`;
  return val;
}

export function FulfillmentOptionsPage({
  groupings,
  slots,
  stockLocations,
  dynamicFields,
  onDynamicFieldsChange,
  predefinedFieldSettings,
  onPredefinedFieldSettingsChange,
  onOpenLogisticPartners,
  onCreateGrouping,
  onUpdateGrouping,
  onDeleteGrouping,
  onViewDeliverySlots,
  onCreateTimeSlot,
}: Props) {
  const [filter, setFilter] = useState("");

  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return groupings;
    return groupings.filter((g) => [g.groupName, g.code, g.deliveryMode].some((x) => x.toLowerCase().includes(q)));
  }, [filter, groupings]);

  // ── Add Grouping modal ───────────────────────────────────────────────────
  const [modalOpen, setModalOpen] = useState(false);
  const [editingGroupingId, setEditingGroupingId] = useState<string | null>(null);
  const [groupName, setGroupName] = useState("");
  const [code, setCode] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [priority, setPriority] = useState<number>(10);
  const [active, setActive] = useState(true);
  const [tenantEnabled, setTenantEnabled] = useState(false);
  const [maxPerTenant, setMaxPerTenant] = useState<number | "">("");
  const [deliveryMode, setDeliveryMode] = useState<FulfillmentDeliveryMode>("Local Delivery");
  const [limitSelectedDistributionCenter, setLimitSelectedDistributionCenter] = useState(false);
  const [stockLocation, setStockLocation] = useState("");

  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  // ── Pre-defined field settings (4.8.8) ───────────────────────────────────
  const [predefinedDraft, setPredefinedDraft] = useState<FulfillmentPredefinedFieldSetting[]>(predefinedFieldSettings);
  const dirtyPredefined = useMemo(() => JSON.stringify(predefinedDraft) !== JSON.stringify(predefinedFieldSettings), [predefinedDraft, predefinedFieldSettings]);

  function togglePredefined(key: FulfillmentPredefinedFieldSetting["key"], enabled: boolean) {
    setPredefinedDraft((prev) => prev.map((f) => (f.key === key ? { ...f, enabled } : f)));
  }

  function savePredefinedSettings() {
    onPredefinedFieldSettingsChange(predefinedDraft);
    showToast("Fulfillment options settings saved.");
  }

  // ── Dynamic fields state ───────────────────────────────────────────────
  const [dfModalOpen, setDfModalOpen] = useState(false);
  const [dfEditingId, setDfEditingId] = useState<string | null>(null);
  const [dfEnabled, setDfEnabled] = useState(true);
  const [dfRequired, setDfRequired] = useState(false);
  const [dfProperty, setDfProperty] = useState("");
  const [dfColumnLabel, setDfColumnLabel] = useState("");
  const [dfFieldName, setDfFieldName] = useState("");
  const [dfFieldType, setDfFieldType] = useState<FulfillmentFieldType>("Text Box");
  const [dfSection, setDfSection] = useState<FulfillmentFieldSection>("Additional Info");
  const [dfDeleteId, setDfDeleteId] = useState<string | null>(null);

  function openAddDynamicField() {
    setDfModalOpen(true);
    setDfEditingId(null);
    setDfEnabled(true);
    setDfRequired(false);
    setDfProperty("");
    setDfColumnLabel("");
    setDfFieldName("");
    setDfFieldType("Text Box");
    setDfSection("Additional Info");
  }

  function openEditDynamicField(f: FulfillmentDynamicField) {
    setDfModalOpen(true);
    setDfEditingId(f.id);
    setDfEnabled(f.enabled);
    setDfRequired(f.required);
    setDfProperty(f.property);
    setDfColumnLabel(f.columnLabel);
    setDfFieldName(f.fieldName);
    setDfFieldType(f.fieldType);
    setDfSection(f.section);
  }

  function handleSaveDynamicField() {
    const entry: FulfillmentDynamicField = {
      id: dfEditingId ?? `df-${Math.random().toString(36).slice(2, 8)}`,
      enabled: dfEnabled,
      required: dfRequired,
      property: dfProperty.trim(),
      columnLabel: dfColumnLabel.trim(),
      fieldName: dfFieldName.trim(),
      fieldType: dfFieldType,
      section: dfSection,
    };
    if (dfEditingId) {
      onDynamicFieldsChange(dynamicFields.map((f) => (f.id === dfEditingId ? entry : f)));
    } else {
      onDynamicFieldsChange([...dynamicFields, entry]);
    }
    setDfModalOpen(false);
    showToast(dfEditingId ? "Field updated." : "Field added.");
  }

  function handleDeleteDynamicField(id: string) {
    onDynamicFieldsChange(dynamicFields.filter((f) => f.id !== id));
    setDfDeleteId(null);
    showToast("Field deleted.");
  }

  function showToast(msg: string) {
    setToast(msg);
    setTimeout(() => setToast(null), 3000);
  }

  // ── Export function ────────────────────────────────────────────────────
  function exportSlotsToCSV() {
    const enabledFieldCount = dynamicFields.filter((f) => f.enabled).length;
    const headers = [
      "Slot ID",
      "Slot Name",
      "Slot Code",
      "Grouping Name",
      "Grouping Code",
      "Delivery Mode",
      "Tenant Enabled",
      "Starting Date",
      "Ending Date",
      "Price",
      "Updated At",
      "Dynamic Fields",
    ];
    const rows = slots.map((s) => {
      const grp = groupings.find((g) => g.id === s.groupingId);
      return [
        s.id,
        s.name,
        s.code,
        grp?.groupName ?? "",
        grp?.code ?? "",
        s.mode,
        grp?.tenantEnabled ? "Yes" : "No",
        s.startingDate,
        s.endingDate,
        s.price,
        s.updatedAt,
        `EnabledFields: ${enabledFieldCount}`,
      ].map(csvEscape);
    });
    const csvContent =
      "\uFEFF" + [headers.map(csvEscape).join(","), ...rows.map((r) => r.join(","))].join("\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "fulfillment-options-export.csv";
    a.click();
    URL.revokeObjectURL(url);
    showToast("Export downloaded.");
  }

  function openAddGrouping() {
    setModalOpen(true);
    setEditingGroupingId(null);
    setGroupName("");
    setCode("");
    setStartDate("");
    setEndDate("");
    setPriority(10);
    setActive(true);
    setTenantEnabled(false);
    setMaxPerTenant("");
    setDeliveryMode("Local Delivery");
    setLimitSelectedDistributionCenter(false);
    setStockLocation("");
  }

  function openEditGrouping(g: FulfillmentGrouping) {
    setModalOpen(true);
    setEditingGroupingId(g.id);
    setGroupName(g.groupName);
    setCode(g.code);
    setStartDate(g.startDate);
    setEndDate(g.endDate);
    setPriority(g.priority);
    setActive(g.active);
    setTenantEnabled(g.tenantEnabled);
    setMaxPerTenant(g.maxPerTenant);
    setDeliveryMode(g.deliveryMode);
    setLimitSelectedDistributionCenter(g.limitSelectedDistributionCenter);
    setStockLocation(g.stockLocation);
  }

  function handleGroupNameBlur() {
    if (!code.trim() && groupName.trim()) setCode(seedCodeFromName(groupName));
  }

  function handleCreateTimeSlot() {
    const next: Omit<FulfillmentGrouping, "id"> = {
      groupName: groupName.trim() || "New Group",
      code: (code.trim() || seedCodeFromName(groupName)).toUpperCase(),
      startDate,
      endDate,
      priority,
      active,
      tenantEnabled,
      maxPerTenant: tenantEnabled ? maxPerTenant : "",
      deliveryMode,
      limitSelectedDistributionCenter,
      stockLocation: limitSelectedDistributionCenter ? stockLocation : "",
    };
    const groupingId = onCreateGrouping(next);
    setModalOpen(false);
    showToast("Grouping created. Redirecting to time slot setup…");
    setTimeout(() => onCreateTimeSlot(groupingId), 400);
  }

  function handleUpdateGrouping() {
    if (!editingGroupingId) return;
    const next: Omit<FulfillmentGrouping, "id"> = {
      groupName: groupName.trim() || "New Group",
      code: (code.trim() || seedCodeFromName(groupName)).toUpperCase(),
      startDate,
      endDate,
      priority,
      active,
      tenantEnabled,
      maxPerTenant: tenantEnabled ? maxPerTenant : "",
      deliveryMode,
      limitSelectedDistributionCenter,
      stockLocation: limitSelectedDistributionCenter ? stockLocation : "",
    };
    onUpdateGrouping(editingGroupingId, next);
    setModalOpen(false);
    showToast("Grouping updated.");
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Fulfillment Options</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface">Fulfillment Options</h1>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Configure delivery modes, delivery slots, and collection locations.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2 rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-3 py-2">
            <IconAccountTree className="h-4 w-4 shrink-0 text-on-surface-variant" />
            <input
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
              placeholder="Search groups…"
              className="w-56 bg-transparent text-xs outline-none placeholder:text-on-surface-variant/50"
            />
          </div>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
            onClick={onOpenLogisticPartners}
            title="Manage logistic partners"
          >
            <IconTune className="h-4 w-4 shrink-0" />
            Logistic Partner Management
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
            onClick={exportSlotsToCSV}
            title="Export all delivery slots to CSV"
          >
            <IconDownload className="h-4 w-4 shrink-0" />
            Export
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
            onClick={openAddGrouping}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Add New Grouping
          </button>
        </div>
      </div>

      {/* Config list body */}
      <div className="flex-1 p-6 pb-24">
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">
              Fulfillment Option Groups
            </h3>
            <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
              {filtered.length} group{filtered.length !== 1 ? "s" : ""}
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Group Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3">Schedule</th>
                  <th className="px-4 py-3 text-right">Priority</th>
                  <th className="px-4 py-3">Mode</th>
                  <th className="px-4 py-3">Tenant</th>
                  <th className="px-4 py-3">Stock Location</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((g) => (
                  <tr key={g.id} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <span className={`h-2 w-2 rounded-full ${g.active ? "bg-green-500" : "bg-outline-variant/40"}`} />
                        <span className="font-semibold text-on-surface">{g.groupName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3.5">
                      <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">
                        {g.code}
                      </code>
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex flex-col text-[11px]">
                        <span className="font-medium text-on-surface">{g.startDate ? g.startDate.replace("T", " ") : "—"}</span>
                        <span className="text-on-surface-variant opacity-60">{g.endDate ? `to ${g.endDate.replace("T", " ")}` : "Permanent"}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3.5 text-right font-mono font-bold text-on-surface">{g.priority}</td>
                    <td className="px-4 py-3.5 text-on-surface-variant">{g.deliveryMode}</td>
                    <td className="px-4 py-3.5">
                      {g.tenantEnabled ? (
                        <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[9px] font-bold uppercase text-primary">
                          Enabled
                        </span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">
                          Disabled
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3.5 text-on-surface-variant">
                      {g.limitSelectedDistributionCenter ? (g.stockLocation || "—") : <span className="italic text-[10px] opacity-70">Any</span>}
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          title="Edit Group Info"
                          className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => openEditGrouping(g)}
                        >
                          <IconEdit className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          title="View Delivery Slots"
                          className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => onViewDeliverySlots(g.id)}
                        >
                          <IconChevronDown className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          title="Delete grouping"
                          className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                          onClick={() => setDeleteConfirmId(g.id)}
                        >
                          <IconDelete className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {filtered.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No groups found. Click “Add New Grouping” to create one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>

        {/* Fulfillment Options Settings (pre-defined fields) */}
        <section className="mt-6 rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Fulfillment Options Settings</h3>
            <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
              {predefinedDraft.filter((f) => f.enabled).length} enabled / {predefinedDraft.length}
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Field</th>
                  <th className="px-4 py-3">Enabled</th>
                </tr>
              </thead>
              <tbody>
                {predefinedDraft.map((f) => (
                  <tr key={f.key} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5 font-semibold text-on-surface">{f.label}</td>
                    <td className="px-4 py-3.5">
                      <label className="inline-flex cursor-pointer items-center gap-2 select-none">
                        <input
                          type="checkbox"
                          className="h-4 w-4 accent-primary"
                          checked={f.enabled}
                          onChange={(e) => togglePredefined(f.key, e.target.checked)}
                        />
                        <span className="text-[11px] font-bold text-on-surface-variant">{f.enabled ? "Enabled" : "Disabled"}</span>
                      </label>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
            <button
              type="button"
              className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
              disabled={!dirtyPredefined}
              onClick={() => setPredefinedDraft(predefinedFieldSettings)}
            >
              Reset
            </button>
            <button
              type="button"
              className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
              disabled={!dirtyPredefined}
              onClick={savePredefinedSettings}
            >
              <IconCheckCircle className="h-4 w-4 shrink-0" />
              Save
            </button>
          </div>
        </section>

        {/* Dynamic Fields config table */}
        <section className="mt-6 rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Dynamic Fields</h3>
            <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
              {dynamicFields.length} field{dynamicFields.length !== 1 ? "s" : ""}
            </span>
          </div>

          {dynamicFields.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left">
                <thead>
                  <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                    <th className="px-5 py-3">Enabled</th>
                    <th className="px-4 py-3">Required</th>
                    <th className="px-4 py-3">Column Label</th>
                    <th className="px-4 py-3">Property</th>
                    <th className="px-4 py-3">Field Name</th>
                    <th className="px-4 py-3">Type</th>
                    <th className="px-4 py-3">Section</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {dynamicFields.map((f) => (
                    <tr key={f.id} className="text-xs hover:bg-surface-container-low transition-colors">
                      <td className="px-5 py-3.5">
                        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${f.enabled ? "bg-primary/10 text-primary" : "bg-surface-container-high text-on-surface-variant/60"}`}>
                          {f.enabled ? "On" : "Off"}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${f.required ? "bg-error/10 text-error" : "bg-surface-container-high text-on-surface-variant/60"}`}>
                          {f.required ? "Yes" : "No"}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 font-semibold text-on-surface">{f.columnLabel}</td>
                      <td className="px-4 py-3.5">
                        <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">{f.property}</code>
                      </td>
                      <td className="px-4 py-3.5 text-on-surface-variant">{f.fieldName}</td>
                      <td className="px-4 py-3.5 text-on-surface-variant">{f.fieldType}</td>
                      <td className="px-4 py-3.5 text-on-surface-variant">{f.section}</td>
                      <td className="px-4 py-3.5">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            type="button"
                            title="Edit field"
                            className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                            onClick={() => openEditDynamicField(f)}
                          >
                            <IconEdit className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            title="Delete field"
                            className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                            onClick={() => setDfDeleteId(f.id)}
                          >
                            <IconDelete className="h-4 w-4" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="px-6 py-10 text-center text-xs italic text-on-surface-variant">
              No dynamic fields configured. Click "Add Field Option" below to add one.
            </p>
          )}

          <div className="border-t border-outline-variant/10 px-6 py-4">
            <button
              type="button"
              className="flex items-center gap-2 rounded-lg border border-dashed border-primary/40 bg-primary/5 px-4 py-2 text-xs font-bold text-primary hover:bg-primary/10 transition-colors"
              onClick={openAddDynamicField}
            >
              <IconAddCircle className="h-4 w-4 shrink-0" />
              Add Field Option
            </button>
          </div>
        </section>
      </div>

      {/* Add / Edit Dynamic Field modal */}
      {dfModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[620px] max-w-[calc(100vw-2rem)] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="flex items-start justify-between gap-4 border-b border-outline-variant/10 px-6 py-5">
              <div>
                <h3 className="text-base font-bold text-on-surface">{dfEditingId ? "Edit Field" : "Add Field Option"}</h3>
                <p className="mt-1 text-xs text-on-surface-variant">Configure a dynamic field for fulfillment option slots.</p>
              </div>
              <button type="button" className="rounded p-2 hover:bg-surface-container transition-colors" onClick={() => setDfModalOpen(false)}>
                <IconClose className="h-5 w-5 text-on-surface-variant" />
              </button>
            </div>

            <div className="max-h-[70vh] overflow-y-auto px-6 py-6 space-y-5">
              <div className="flex gap-6">
                <label className="flex cursor-pointer items-center gap-2 select-none">
                  <input type="checkbox" className="h-4 w-4 accent-primary" checked={dfEnabled} onChange={(e) => setDfEnabled(e.target.checked)} />
                  <span className="text-xs font-bold text-on-surface">Enabled</span>
                </label>
                <label className="flex cursor-pointer items-center gap-2 select-none">
                  <input type="checkbox" className="h-4 w-4 accent-primary" checked={dfRequired} onChange={(e) => setDfRequired(e.target.checked)} />
                  <span className="text-xs font-bold text-on-surface">Required</span>
                </label>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Column Label <span className="text-error">*</span></label>
                  <input className={inputBase} value={dfColumnLabel} onChange={(e) => setDfColumnLabel(e.target.value)} placeholder="e.g. Delivery Note" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Property <span className="text-error">*</span></label>
                  <input className={inputBase} value={dfProperty} onChange={(e) => setDfProperty(e.target.value)} placeholder="e.g. deliveryNote" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Field Name</label>
                  <input className={inputBase} value={dfFieldName} onChange={(e) => setDfFieldName(e.target.value)} placeholder="e.g. delivery_note" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Field Type</label>
                  <select className={inputBase} value={dfFieldType} onChange={(e) => setDfFieldType(e.target.value as FulfillmentFieldType)}>
                    {FIELD_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                  </select>
                </div>
                <div className="md:col-span-2">
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Section</label>
                  <select className={inputBase} value={dfSection} onChange={(e) => setDfSection(e.target.value as FulfillmentFieldSection)}>
                    {FIELD_SECTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
                  </select>
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDfModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
                disabled={!dfColumnLabel.trim() || !dfProperty.trim()}
                onClick={handleSaveDynamicField}
              >
                <IconCheckCircle className="h-4 w-4 shrink-0" />
                {dfEditingId ? "Update" : "Save Field"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete dynamic field confirm */}
      {dfDeleteId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="px-6 py-5">
              <h3 className="text-sm font-bold text-on-surface">Delete Dynamic Field</h3>
              <p className="mt-1 text-xs text-on-surface-variant">This field will be removed from the configuration and will no longer appear in slot editors.</p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDfDeleteId(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-6 py-2.5 text-xs font-bold uppercase text-on-error shadow-md transition-all hover:opacity-90"
                onClick={() => handleDeleteDynamicField(dfDeleteId)}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Add grouping modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[760px] max-w-[calc(100vw-2rem)] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="flex items-start justify-between gap-4 border-b border-outline-variant/10 px-6 py-5">
              <div>
                <h3 className="text-base font-bold text-on-surface">{editingGroupingId ? "Edit Grouping Info" : "Add Grouping"}</h3>
                <p className="mt-1 text-xs text-on-surface-variant">
                  {editingGroupingId ? "Update the fulfillment option group details." : "Create a fulfillment option group and proceed to time slot setup."}
                </p>
              </div>
              <button type="button" className="rounded p-2 hover:bg-surface-container transition-colors" onClick={() => setModalOpen(false)}>
                <IconClose className="h-5 w-5 text-on-surface-variant" />
              </button>
            </div>

            <div className="max-h-[70vh] overflow-y-auto px-6 py-6 space-y-6">
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Group Name <span className="text-error">*</span>
                  </label>
                  <input className={inputBase} value={groupName} onChange={(e) => setGroupName(e.target.value)} onBlur={handleGroupNameBlur} />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Code <span className="text-error">*</span>
                  </label>
                  <input className={inputBase} value={code} onChange={(e) => setCode(e.target.value)} placeholder="AUTO-GENERATED" />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Start Date</label>
                  <input type="datetime-local" className={inputBase} value={startDate} onChange={(e) => setStartDate(e.target.value)} />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">End Date</label>
                  <input type="datetime-local" className={inputBase} value={endDate} onChange={(e) => setEndDate(e.target.value)} />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Priority</label>
                  <input type="number" className={inputBase} value={priority} onChange={(e) => setPriority(Number(e.target.value))} />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                  <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={active} onChange={(e) => setActive(e.target.checked)} />
                  <div>
                    <p className="text-xs font-bold text-on-surface">Active</p>
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">Only active groups can be used.</p>
                  </div>
                </label>
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                  <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={tenantEnabled} onChange={(e) => setTenantEnabled(e.target.checked)} />
                  <div>
                    <p className="text-xs font-bold text-on-surface">Tenant</p>
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">Enable tenant-scoped delivery slot groups.</p>
                  </div>
                </label>
              </div>

              {tenantEnabled && (
                <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                  <div className="rounded-lg border border-primary/20 bg-primary/5 p-4 flex items-start gap-3">
                    <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
                    <p className="text-xs text-on-surface-variant leading-relaxed">
                      Once the fulfillment option group is created, tenants can only update delivery slots assigned to the tenant.
                    </p>
                  </div>
                  <div>
                    <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                      Max. Fulfillment Options (per Tenant)
                    </label>
                    <input
                      type="number"
                      min="1"
                      className={inputBase}
                      value={maxPerTenant}
                      onChange={(e) => setMaxPerTenant(e.target.value === "" ? "" : Number(e.target.value))}
                      placeholder="e.g. 5"
                    />
                  </div>
                </div>
              )}

              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Delivery Mode</label>
                  <select className={inputBase} value={deliveryMode} onChange={(e) => setDeliveryMode(e.target.value as FulfillmentDeliveryMode)}>
                    {DELIVERY_MODES.map((m) => (
                      <option key={m} value={m}>
                        {m}
                      </option>
                    ))}
                  </select>
                </div>
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                  <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={limitSelectedDistributionCenter} onChange={(e) => setLimitSelectedDistributionCenter(e.target.checked)} />
                  <div>
                    <p className="text-xs font-bold text-on-surface">Limit Selected Distribution Center</p>
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                      Issue stocks from a specific stock location.\n                    </p>
                  </div>
                </label>
              </div>

              {limitSelectedDistributionCenter && (
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Stock Location <span className="text-error">*</span>
                  </label>
                  <select className={inputBase} value={stockLocation} onChange={(e) => setStockLocation(e.target.value)}>
                    <option value="">Select stock location…</option>
                    {stockLocations
                      .filter((s) => s.active)
                      .map((s) => (
                        <option key={s.id} value={s.code}>
                          {s.name} ({s.code})
                        </option>
                      ))}
                  </select>
                </div>
              )}
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setModalOpen(false)}
              >
                Cancel
              </button>
              {editingGroupingId ? (
                <button
                  type="button"
                  className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
                  disabled={!groupName.trim() || !(code.trim() || groupName.trim())}
                  onClick={handleUpdateGrouping}
                >
                  <IconCheckCircle className="h-4 w-4 shrink-0" />
                  Update
                </button>
              ) : (
                <button
                  type="button"
                  className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
                  disabled={!groupName.trim() || !(code.trim() || groupName.trim())}
                  onClick={handleCreateTimeSlot}
                >
                  <IconCheckCircle className="h-4 w-4 shrink-0" />
                  Create Time Slot
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Delete confirm modal */}
      {deleteConfirmId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="px-6 py-5">
              <h3 className="text-sm font-bold text-on-surface">Delete Grouping</h3>
              <p className="mt-1 text-xs text-on-surface-variant">This will remove the grouping from the list.</p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button type="button" className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors" onClick={() => setDeleteConfirmId(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={() => {
                  onDeleteGrouping(deleteConfirmId);
                  setDeleteConfirmId(null);
                  showToast("Grouping deleted.");
                }}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
          <button type="button" className="rounded p-1 hover:bg-surface-container transition-colors" onClick={() => setToast(null)}>
            <IconClose className="h-4 w-4 text-on-surface-variant" />
          </button>
        </div>
      )}
    </div>
  );
}
