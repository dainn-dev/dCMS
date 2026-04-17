import { useEffect, useMemo, useState } from "react";
import {
  IconAddCircle,
  IconArrowBack,
  IconCheckCircle,
  IconClose,
  IconDelete,
  IconEdit,
  IconInfo,
} from "../../orders/icons";
import type { BrandListRow } from "./BrandsPage";
import type { CollectionLocation } from "../EStoreApp";

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";

type Props = {
  locations: CollectionLocation[];
  onChange: (next: CollectionLocation[]) => void;
  brands: BrandListRow[];
  onBack: () => void;
};

function seedId() {
  return `LOC_${Math.random().toString(36).slice(2, 8).toUpperCase()}`;
}

const COUNTRIES = ["Singapore", "Malaysia", "Thailand", "Vietnam", "Indonesia", "Philippines"] as const;

export function CollectionLocationsPage({ locations, onChange, brands, onBack }: Props) {
  const [filter, setFilter] = useState("");
  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return locations;
    const address = (l: CollectionLocation) =>
      [l.address1, l.address2, l.address3, l.postalCode, l.country].filter((x) => (x ?? "").trim()).join(" ");
    return locations.filter((l) => [l.id, l.name, address(l)].some((x) => (x ?? "").toLowerCase().includes(q)));
  }, [filter, locations]);

  const [modalOpen, setModalOpen] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [locationId, setLocationId] = useState("");
  const [name, setName] = useState("");
  const [brandCodes, setBrandCodes] = useState<string[]>([]);
  const [address1, setAddress1] = useState("");
  const [address2, setAddress2] = useState("");
  const [address3, setAddress3] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [country, setCountry] = useState<(typeof COUNTRIES)[number]>("Singapore");
  const [geoLat, setGeoLat] = useState("");
  const [geoLng, setGeoLng] = useState("");
  const [desktopImageSrc, setDesktopImageSrc] = useState<string | undefined>(undefined);
  const [desktopImageName, setDesktopImageName] = useState<string | undefined>(undefined);
  const [mobileImageSrc, setMobileImageSrc] = useState<string | undefined>(undefined);
  const [mobileImageName, setMobileImageName] = useState<string | undefined>(undefined);
  const [active, setActive] = useState(true);
  const [openingHours, setOpeningHours] = useState("10:00");
  const [closingHours, setClosingHours] = useState("22:00");
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function openAdd() {
    setModalOpen(true);
    setEditId(null);
    setLocationId(seedId());
    setName("");
    setBrandCodes([]);
    setAddress1("");
    setAddress2("");
    setAddress3("");
    setPostalCode("");
    setCountry("Singapore");
    setGeoLat("");
    setGeoLng("");
    setDesktopImageSrc(undefined);
    setDesktopImageName(undefined);
    setMobileImageSrc(undefined);
    setMobileImageName(undefined);
    setActive(true);
    setOpeningHours("10:00");
    setClosingHours("22:00");
  }

  function openEdit(loc: CollectionLocation) {
    setModalOpen(true);
    setEditId(loc.id);
    setLocationId(loc.id);
    setName(loc.name);
    setBrandCodes(loc.brandCodes ?? []);
    setAddress1(loc.address1 ?? "");
    setAddress2(loc.address2 ?? "");
    setAddress3(loc.address3 ?? "");
    setPostalCode(loc.postalCode ?? "");
    setCountry((loc.country as (typeof COUNTRIES)[number]) ?? "Singapore");
    setGeoLat(loc.geoLat ?? "");
    setGeoLng(loc.geoLng ?? "");
    setDesktopImageSrc(loc.desktopImageSrc);
    setDesktopImageName(loc.desktopImageName);
    setMobileImageSrc(loc.mobileImageSrc);
    setMobileImageName(loc.mobileImageName);
    setActive(loc.active);
    setOpeningHours(loc.openingHours ?? "10:00");
    setClosingHours(loc.closingHours ?? "22:00");
  }

  function toggleBrand(code: string) {
    setBrandCodes((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]));
  }

  function detectGeo() {
    if (!navigator.geolocation) {
      setGeoLat("1.3000");
      setGeoLng("103.8000");
      setToast("Geolocation is not available in this browser. Mock coordinates applied.");
      return;
    }
    setToast("Detecting geolocation…");
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setGeoLat(pos.coords.latitude.toFixed(6));
        setGeoLng(pos.coords.longitude.toFixed(6));
        setToast("Geolocation detected.");
      },
      () => {
        setGeoLat("1.3000");
        setGeoLng("103.8000");
        setToast("Unable to detect location. Mock coordinates applied.");
      },
      { enableHighAccuracy: false, timeout: 8000 }
    );
  }

  function onPickImage(kind: "desktop" | "mobile", file: File | null) {
    if (!file) return;
    const url = URL.createObjectURL(file);
    if (kind === "desktop") {
      if (desktopImageSrc) URL.revokeObjectURL(desktopImageSrc);
      setDesktopImageSrc(url);
      setDesktopImageName(file.name);
    } else {
      if (mobileImageSrc) URL.revokeObjectURL(mobileImageSrc);
      setMobileImageSrc(url);
      setMobileImageName(file.name);
    }
  }

  function save() {
    const record: CollectionLocation = {
      id: editId ?? (locationId.trim() || seedId()),
      name: name.trim() || "Unnamed Location",
      brandCodes,
      address1: address1.trim(),
      address2: address2.trim(),
      address3: address3.trim(),
      postalCode: postalCode.trim(),
      country,
      geoLat: geoLat.trim(),
      geoLng: geoLng.trim(),
      desktopImageSrc,
      desktopImageName,
      mobileImageSrc,
      mobileImageName,
      active,
      openingHours,
      closingHours,
    };
    onChange(
      editId ? locations.map((l) => (l.id === editId ? record : l)) : [record, ...locations]
    );
    setModalOpen(false);
    setToast(editId ? "Collection location updated." : "Collection location created.");
  }

  function removeConfirmed(id: string) {
    onChange(locations.filter((l) => l.id !== id));
    setToast("Collection location deleted.");
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Fulfillment Options
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Collection Location Manager</h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Manage collection points used by Store Collection fulfillment modes.
          </p>
        </div>
        <button
          type="button"
          className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
          onClick={openAdd}
        >
          <IconAddCircle className="h-4 w-4 shrink-0" />
          Add Collection Location
        </button>
      </div>

      <div className="flex-1 space-y-6 p-6 pb-24">
        <div className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
          <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Collection locations are selectable in Fulfillment Options Management. Address is auto-populated when selected.
          </p>
        </div>

        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Locations</h3>
            <div className="ml-auto flex items-center gap-3">
              <input
                className={`${inputBase} w-64`}
                placeholder="Search name or address…"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              />
              <span className="rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
                {filtered.length} location{filtered.length !== 1 ? "s" : ""}
              </span>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">ID</th>
                  <th className="px-5 py-3">Name</th>
                  <th className="px-4 py-3">Address</th>
                  <th className="px-4 py-3">Hours</th>
                  <th className="px-4 py-3">Active</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((l) => (
                  <tr key={l.id} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5">
                      <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">
                        {l.id}
                      </code>
                    </td>
                    <td className="px-5 py-3.5 font-semibold text-on-surface">{l.name}</td>
                    <td className="px-4 py-3.5 text-on-surface-variant">
                      {[l.address1, l.address2, l.address3].filter((x) => (x ?? "").trim()).join(", ") || "—"}
                    </td>
                    <td className="px-4 py-3.5 font-mono text-[11px] text-on-surface-variant">
                      {(l.openingHours ?? "—")} - {(l.closingHours ?? "—")}
                    </td>
                    <td className="px-4 py-3.5">
                      {l.active ? (
                        <span className="rounded-full bg-secondary-container/20 px-2 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">Yes</span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">No</span>
                      )}
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          title="Edit"
                          className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => openEdit(l)}
                        >
                          <IconEdit className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          title="Delete"
                          className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                          onClick={() => setDeleteConfirmId(l.id)}
                        >
                          <IconDelete className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {filtered.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No locations found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[640px] max-w-[calc(100vw-2rem)] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="flex items-start justify-between gap-4 border-b border-outline-variant/10 px-6 py-5">
              <div>
                <h3 className="text-base font-bold text-on-surface">{editId ? "Edit Collection Location" : "Add Collection Location"}</h3>
                <p className="mt-1 text-xs text-on-surface-variant">Configure a collection point for Store Collection mode.</p>
              </div>
              <button type="button" className="rounded p-2 hover:bg-surface-container transition-colors" onClick={() => setModalOpen(false)}>
                <IconClose className="h-5 w-5 text-on-surface-variant" />
              </button>
            </div>

            <div className="max-h-[70vh] overflow-y-auto px-6 py-6 space-y-6">
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Collection Location ID <span className="text-error">*</span>
                  </label>
                  <input
                    className={`${inputBase} ${editId ? "bg-surface-container-high" : ""}`}
                    value={locationId}
                    onChange={(e) => setLocationId(e.target.value.toUpperCase())}
                    disabled={!!editId}
                    placeholder="e.g. LOC_MAIN"
                  />
                  <p className="mt-1 text-[10px] text-on-surface-variant">Once created, Collection Location ID cannot be updated.</p>
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Collection Location Name <span className="text-error">*</span>
                  </label>
                  <input className={inputBase} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Main Store" />
                </div>
              </div>

              <div>
                <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-2">Brands</label>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  {brands.map((b) => (
                    <label
                      key={b.code}
                      className="flex cursor-pointer items-center gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 hover:border-primary/30 transition-colors select-none"
                    >
                      <input type="checkbox" className="h-4 w-4 accent-primary" checked={brandCodes.includes(b.code)} onChange={() => toggleBrand(b.code)} />
                      <span className="text-xs font-semibold text-on-surface">{b.name}</span>
                      <span className="ml-auto text-[10px] font-mono text-on-surface-variant">{b.code}</span>
                    </label>
                  ))}
                </div>
                <p className="mt-1 text-[10px] text-on-surface-variant">Select one or more brands to associate this collection location with.</p>
              </div>

              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Address 1</label>
                  <input className={inputBase} value={address1} onChange={(e) => setAddress1(e.target.value)} placeholder="Street / building" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Address 2</label>
                  <input className={inputBase} value={address2} onChange={(e) => setAddress2(e.target.value)} placeholder="Unit / floor" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Address 3</label>
                  <input className={inputBase} value={address3} onChange={(e) => setAddress3(e.target.value)} placeholder="Additional line" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Postal Code</label>
                  <input className={inputBase} value={postalCode} onChange={(e) => setPostalCode(e.target.value)} placeholder="e.g. 123456" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Country</label>
                  <select className={inputBase} value={country} onChange={(e) => setCountry(e.target.value as (typeof COUNTRIES)[number])}>
                    {COUNTRIES.map((c) => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Geolocation</label>
                  <div className="grid grid-cols-2 gap-3">
                    <input className={inputBase} value={geoLat} onChange={(e) => setGeoLat(e.target.value)} placeholder="Lat" />
                    <input className={inputBase} value={geoLng} onChange={(e) => setGeoLng(e.target.value)} placeholder="Lng" />
                  </div>
                  <button
                    type="button"
                    className="mt-2 rounded-md border border-outline-variant/30 px-3 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                    onClick={detectGeo}
                  >
                    Detect current location
                  </button>
                </div>
              </div>

              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Opening Hours</label>
                  <input type="time" className={inputBase} value={openingHours} onChange={(e) => setOpeningHours(e.target.value)} />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Closing Hours</label>
                  <input type="time" className={inputBase} value={closingHours} onChange={(e) => setClosingHours(e.target.value)} />
                </div>
              </div>

              <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={active} onChange={(e) => setActive(e.target.checked)} />
                <div>
                  <p className="text-xs font-bold text-on-surface">Active</p>
                  <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">Only active locations are selectable.</p>
                </div>
              </label>

              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Desktop Image</label>
                  <input type="file" accept="image/*" className={inputBase} onChange={(e) => onPickImage("desktop", e.target.files?.[0] ?? null)} />
                  {desktopImageSrc && (
                    <div className="mt-2 flex items-center gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-3">
                      <img src={desktopImageSrc} alt={desktopImageName ?? "Desktop image"} className="h-12 w-12 rounded object-cover" />
                      <div className="min-w-0">
                        <p className="truncate text-xs font-semibold text-on-surface">{desktopImageName}</p>
                        <p className="text-[10px] text-on-surface-variant">Preview (UI-only)</p>
                      </div>
                    </div>
                  )}
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Mobile Image</label>
                  <input type="file" accept="image/*" className={inputBase} onChange={(e) => onPickImage("mobile", e.target.files?.[0] ?? null)} />
                  {mobileImageSrc && (
                    <div className="mt-2 flex items-center gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-3">
                      <img src={mobileImageSrc} alt={mobileImageName ?? "Mobile image"} className="h-12 w-12 rounded object-cover" />
                      <div className="min-w-0">
                        <p className="truncate text-xs font-semibold text-on-surface">{mobileImageName}</p>
                        <p className="text-[10px] text-on-surface-variant">Preview (UI-only)</p>
                      </div>
                    </div>
                  )}
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
                onClick={save}
                disabled={!locationId.trim() || !name.trim()}
              >
                <IconCheckCircle className="h-4 w-4 shrink-0" />
                Save
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete confirm modal */}
      {deleteConfirmId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="px-6 py-5">
              <h3 className="text-sm font-bold text-on-surface">Delete Collection Location</h3>
              <p className="mt-1 text-xs text-on-surface-variant">
                This will permanently remove the collection location from the list.
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteConfirmId(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={() => {
                  removeConfirmed(deleteConfirmId);
                  setDeleteConfirmId(null);
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
        </div>
      )}
    </div>
  );
}

