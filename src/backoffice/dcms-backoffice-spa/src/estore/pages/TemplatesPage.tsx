import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { deleteTemplate, listTemplates, previewTemplate, putTemplate, type TemplateRow } from "../api/templatesApi";
import { useLanguages } from "../LanguageContext";
import {
  buildEffectiveCatalog,
  deleteCatalogEntry,
  fetchTemplateCatalog,
  TEMPLATE_CATALOG,
  upsertCatalogEntry,
  type TemplateCatalogEntry,
  type TemplateVar,
} from "../templateCatalog";
import {
  IconFormatBold,
  IconFormatItalic,
  IconFormatListBulleted,
  IconFormatUnderlined,
  IconImage,
  IconLink,
} from "../../orders/icons";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

const langPill = (iso: string) => iso.split("-")[0].toUpperCase();
const varToken = (path: string) => `{{ model.${path} }}`;

// ── Insert-variable picker ──────────────────────────────────────────────────
function VariableMenu({ variables, onPick }: { variables: TemplateVar[]; onPick: (v: TemplateVar) => void }) {
  return (
    <select
      className="rounded border border-outline-variant/30 bg-surface px-1.5 py-1 text-[11px] text-on-surface-variant hover:bg-surface-container-high focus:ring-1 focus:ring-primary outline-none"
      value=""
      onChange={(e) => {
        const v = variables.find((x) => x.path === e.target.value);
        if (v) onPick(v);
        e.target.value = "";
      }}
      title="Insert a personalisation variable"
    >
      <option value="">+ Insert variable…</option>
      {variables.map((v) => (
        <option key={v.path} value={v.path}>
          {v.label}
        </option>
      ))}
    </select>
  );
}

// ── WYSIWYG body editor (contentEditable + toolbar + insert variable) ────────
function RichBodyEditor({
  initialHtml,
  variables,
  onChange,
  minHeight = 300,
}: {
  initialHtml: string;
  variables: TemplateVar[];
  onChange: (html: string) => void;
  minHeight?: number;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [isBold, setIsBold] = useState(false);
  const [isItalic, setIsItalic] = useState(false);
  const [isUnderline, setIsUnderline] = useState(false);

  useEffect(() => {
    if (ref.current) ref.current.innerHTML = initialHtml;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const syncState = useCallback(() => {
    setIsBold(document.queryCommandState("bold"));
    setIsItalic(document.queryCommandState("italic"));
    setIsUnderline(document.queryCommandState("underline"));
  }, []);

  const emit = useCallback(() => {
    const el = ref.current;
    if (!el) return;
    const html = el.innerHTML;
    onChange(html === "<br>" ? "" : html);
  }, [onChange]);

  const fmt = useCallback(
    (cmd: string) => {
      ref.current?.focus();
      document.execCommand(cmd, false, undefined);
      syncState();
      emit();
    },
    [syncState, emit],
  );

  const addLink = useCallback(() => {
    const url = window.prompt("Link URL");
    if (!url?.trim()) return;
    ref.current?.focus();
    document.execCommand("createLink", false, url.trim());
    emit();
  }, [emit]);

  const addImage = useCallback(() => {
    const url = window.prompt("Image URL");
    if (!url?.trim()) return;
    ref.current?.focus();
    document.execCommand("insertImage", false, url.trim());
    emit();
  }, [emit]);

  const insertVar = useCallback(
    (v: TemplateVar) => {
      ref.current?.focus();
      document.execCommand("insertText", false, varToken(v.path));
      emit();
    },
    [emit],
  );

  const btn = (active: boolean) =>
    `p-1.5 rounded transition-colors ${active ? "bg-primary/15 text-primary" : "hover:bg-surface-container-high text-on-surface"}`;
  const guard = (fn: () => void) => (e: React.MouseEvent) => {
    e.preventDefault();
    fn();
  };

  return (
    <div className="rounded-md border border-outline-variant/20 bg-surface overflow-hidden">
      <div className="flex flex-wrap items-center gap-1 border-b border-outline-variant/20 bg-surface-container-low p-2">
        <button type="button" className={btn(isBold)} aria-label="Bold" onMouseDown={guard(() => fmt("bold"))}>
          <IconFormatBold className="h-4 w-4" />
        </button>
        <button type="button" className={btn(isItalic)} aria-label="Italic" onMouseDown={guard(() => fmt("italic"))}>
          <IconFormatItalic className="h-4 w-4" />
        </button>
        <button type="button" className={btn(isUnderline)} aria-label="Underline" onMouseDown={guard(() => fmt("underline"))}>
          <IconFormatUnderlined className="h-4 w-4" />
        </button>
        <div className="mx-1 h-4 w-px bg-outline-variant/40" aria-hidden />
        <button type="button" className={btn(false)} aria-label="Bullet list" onMouseDown={guard(() => fmt("insertUnorderedList"))}>
          <IconFormatListBulleted className="h-4 w-4" />
        </button>
        <button type="button" className={btn(false)} aria-label="Link" onMouseDown={guard(addLink)}>
          <IconLink className="h-4 w-4" />
        </button>
        <button type="button" className={btn(false)} aria-label="Image" onMouseDown={guard(addImage)}>
          <IconImage className="h-4 w-4" />
        </button>
        <div className="mx-1 h-4 w-px bg-outline-variant/40" aria-hidden />
        <VariableMenu variables={variables} onPick={insertVar} />
      </div>
      <div
        ref={ref}
        contentEditable
        suppressContentEditableWarning
        role="textbox"
        aria-multiline
        onInput={emit}
        onKeyUp={syncState}
        onMouseUp={syncState}
        className="prose prose-sm max-w-none"
        style={{ minHeight, padding: "10px 12px", fontSize: 13, outline: "none", cursor: "text", overflowWrap: "break-word" }}
      />
    </div>
  );
}

export function TemplatesPage({ tenantId, storeId, authToken }: Props) {
  const { languages } = useLanguages();
  const [rows, setRows] = useState<TemplateRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [catalogBase, setCatalogBase] = useState<TemplateCatalogEntry[]>(TEMPLATE_CATALOG);
  const catalog = useMemo(() => buildEffectiveCatalog(rows, catalogBase), [rows, catalogBase]);
  const [selectedId, setSelectedId] = useState<string>(() => TEMPLATE_CATALOG[0]?.id ?? "");
  const entry: TemplateCatalogEntry = useMemo(
    () => catalog.find((c) => c.id === selectedId) ?? catalog[0],
    [catalog, selectedId],
  );

  const defaultIso = useMemo(
    () => languages.find((l) => l.isDefault)?.isoCode ?? languages[0]?.isoCode ?? "en-US",
    [languages],
  );
  const [activeIso, setActiveIso] = useState<string>("");
  const resolvedIso = activeIso || defaultIso;

  const [draftSubject, setDraftSubject] = useState("");
  const [draftBody, setDraftBody] = useState("");
  const [editorKey, setEditorKey] = useState(0);

  const [previewSubject, setPreviewSubject] = useState("");
  const [previewBody, setPreviewBody] = useState("");
  const [previewErr, setPreviewErr] = useState<string | null>(null);

  const subjectRef = useRef<HTMLInputElement>(null);

  // ── Message-type management (settings) state ──
  const [mode, setMode] = useState<"content" | "settings">("content");
  const [creating, setCreating] = useState(false);
  const [defName, setDefName] = useState("");
  const [defDescription, setDefDescription] = useState("");
  const [defKey, setDefKey] = useState("");
  const [defChannel, setDefChannel] = useState<TemplateRow["channel"]>("email");
  const [defVars, setDefVars] = useState<TemplateVar[]>([]);

  const rowFor = useCallback(
    (e: TemplateCatalogEntry, iso: string) =>
      rows.find((r) => r.key === e.key && r.locale === iso && r.channel === e.channel && r.tenantId === (tenantId ?? null)),
    [rows, tenantId],
  );
  const existing = entry ? rowFor(entry, resolvedIso) : undefined;
  const entryHasAnyRow = useMemo(
    () => (entry ? rows.some((r) => r.key === entry.key && r.channel === entry.channel) : false),
    [rows, entry],
  );
  const savedLocales = useMemo(
    () => new Set(entry ? rows.filter((r) => r.key === entry.key && r.channel === entry.channel).map((r) => r.locale) : []),
    [rows, entry],
  );

  async function refresh(): Promise<void> {
    if (!tenantId) return;
    const data = await listTemplates(tenantId, storeId, authToken);
    setRows(data);
  }

  async function refreshCatalog(): Promise<TemplateCatalogEntry[]> {
    if (!tenantId) return catalogBase;
    const entries = await fetchTemplateCatalog(tenantId, storeId, authToken);
    if (entries.length) setCatalogBase(entries);
    return entries;
  }

  // ── Settings (message-type) actions ──
  function openSettings() {
    if (!entry) return;
    setCreating(false);
    setDefName(entry.name);
    setDefDescription(entry.description);
    setDefKey(entry.key);
    setDefChannel(entry.channel);
    setDefVars(entry.variables.map((v) => ({ ...v })));
    setError(null);
    setNotice(null);
    setMode("settings");
  }

  function newMessage() {
    setCreating(true);
    setDefName("");
    setDefDescription("");
    setDefKey("");
    setDefChannel("email");
    setDefVars([{ path: "", label: "", sample: "" }]);
    setError(null);
    setNotice(null);
    setMode("settings");
  }

  function patchVar(i: number, patch: Partial<TemplateVar>) {
    setDefVars((prev) => prev.map((v, idx) => (idx === i ? { ...v, ...patch } : v)));
  }
  function addVar() {
    setDefVars((prev) => [...prev, { path: "", label: "", sample: "" }]);
  }
  function removeVar(i: number) {
    setDefVars((prev) => prev.filter((_, idx) => idx !== i));
  }

  async function handleSaveSettings() {
    if (!tenantId) return;
    const name = defName.trim();
    const key = defKey.trim();
    if (!name) {
      setError("Message name is required.");
      return;
    }
    if (!key) {
      setError("Message key is required (e.g. order.confirmation).");
      return;
    }
    const cleanVars = defVars
      .map((v) => ({ path: v.path.trim(), label: (v.label || v.path).trim(), sample: v.sample }))
      .filter((v) => v.path);
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await upsertCatalogEntry(
        tenantId,
        storeId,
        {
          name,
          description: defDescription.trim(),
          key,
          channel: defChannel,
          variables: cleanVars,
          defaultSubject: creating ? undefined : entry?.defaultSubject,
          defaultBody: creating ? undefined : entry?.defaultBody,
        },
        authToken,
      );
      await refreshCatalog();
      setSelectedId(`${key}|${defChannel}`);
      setActiveIso("");
      setMode("content");
      setNotice(creating ? "Message created." : "Message settings saved.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save message settings");
    } finally {
      setBusy(false);
    }
  }

  async function handleDeleteMessage() {
    if (!tenantId || !entry || creating) return;
    if (!window.confirm(`Delete the “${entry.name}” message type and all of its saved content? This cannot be undone.`)) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await deleteCatalogEntry(tenantId, storeId, { key: entry.key, channel: entry.channel }, authToken);
      const entries = await refreshCatalog();
      await refresh();
      setSelectedId(entries[0]?.id ?? "");
      setActiveIso("");
      setMode("content");
      setNotice("Message deleted.");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to delete message");
    } finally {
      setBusy(false);
    }
  }

  // Initial load
  useEffect(() => {
    let cancelled = false;
    async function run() {
      if (!tenantId) return;
      setLoading(true);
      setError(null);
      try {
        const data = await listTemplates(tenantId, storeId, authToken);
        if (!cancelled) setRows(data);
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : "Failed to load templates");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    run();
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken]);

  // Load the config-driven catalog of template types (falls back to the static list).
  useEffect(() => {
    let cancelled = false;
    if (!tenantId) return;
    fetchTemplateCatalog(tenantId, storeId, authToken)
      .then((entries) => {
        if (!cancelled && entries.length) setCatalogBase(entries);
      })
      .catch(() => {
        /* keep static fallback */
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken]);

  // Load the saved content for the active (template, language) into the draft.
  useEffect(() => {
    if (!entry) return;
    const row = rowFor(entry, resolvedIso);
    if (row) {
      setDraftSubject(row.subject ?? "");
      setDraftBody(row.body ?? "");
    } else if (!entryHasAnyRow && resolvedIso === defaultIso) {
      // Brand-new template: offer starter content in the default language only.
      setDraftSubject(entry.defaultSubject ?? "");
      setDraftBody(entry.defaultBody ?? "");
    } else {
      setDraftSubject("");
      setDraftBody("");
    }
    setEditorKey((k) => k + 1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entry?.id, resolvedIso, rows]);

  // Auto-dismiss success notice.
  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 3000);
    return () => clearTimeout(t);
  }, [notice]);

  // Live preview (debounced server-side Scriban render with sample data).
  useEffect(() => {
    if (!tenantId || !entry) return;
    if (!draftBody.trim()) {
      setPreviewBody("");
      setPreviewSubject(draftSubject);
      setPreviewErr(null);
      return;
    }
    let cancelled = false;
    const handle = setTimeout(async () => {
      try {
        const sampleModel = Object.fromEntries(entry.variables.map((v) => [v.path, v.sample]));
        const res = await previewTemplate(
          tenantId,
          storeId,
          { key: entry.key, locale: resolvedIso, channel: entry.channel, subject: draftSubject, body: draftBody, sampleModel },
          authToken,
        );
        if (cancelled) return;
        setPreviewSubject(typeof res.subject === "string" ? res.subject : draftSubject);
        setPreviewBody(typeof res.body === "string" ? res.body : String(res.body));
        setPreviewErr(null);
      } catch (e) {
        if (!cancelled) setPreviewErr(e instanceof Error ? e.message : "Preview failed");
      }
    }, 450);
    return () => {
      cancelled = true;
      clearTimeout(handle);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId, storeId, authToken, entry?.id, resolvedIso, draftSubject, draftBody]);

  function selectTemplate(id: string) {
    setError(null);
    setNotice(null);
    setSelectedId(id);
    setActiveIso("");
  }

  function insertSubjectVar(v: TemplateVar) {
    const el = subjectRef.current;
    const token = varToken(v.path);
    if (!el) {
      setDraftSubject((s) => s + token);
      return;
    }
    const start = el.selectionStart ?? draftSubject.length;
    const end = el.selectionEnd ?? draftSubject.length;
    const next = draftSubject.slice(0, start) + token + draftSubject.slice(end);
    setDraftSubject(next);
    requestAnimationFrame(() => {
      el.focus();
      const caret = start + token.length;
      el.setSelectionRange(caret, caret);
    });
  }

  async function handleSave() {
    if (!tenantId || !entry) return;
    if (!draftBody.trim()) {
      setError("Email content can't be empty.");
      return;
    }
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      await putTemplate(
        tenantId,
        storeId,
        { id: existing?.id, key: entry.key, locale: resolvedIso, channel: entry.channel, subject: draftSubject, body: draftBody, modelVersion: existing?.modelVersion ?? 1 },
        authToken,
      );
      await refresh();
      setNotice(`Saved “${entry.name}” (${langPill(resolvedIso)}).`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save template");
    } finally {
      setBusy(false);
    }
  }

  async function handleDelete() {
    if (!tenantId || !entry || !existing) return;
    if (!window.confirm(`Reset “${entry.name}” (${langPill(resolvedIso)}) back to the system default? This deletes your customised version.`)) return;
    setError(null);
    setNotice(null);
    setBusy(true);
    try {
      await deleteTemplate(tenantId, storeId, { key: entry.key, locale: resolvedIso, channel: entry.channel }, authToken);
      await refresh();
      setNotice(`Reset “${entry.name}” (${langPill(resolvedIso)}).`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to reset template");
    } finally {
      setBusy(false);
    }
  }

  const tabLanguages = languages.length ? languages : [{ isoCode: defaultIso, name: defaultIso, isDefault: true, isMandatory: true }];

  return (
    <section className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Email Templates</h1>
          <p className="text-xs text-on-surface-variant">
            Customise the emails your customers receive. Pick a message, edit it, and see a live preview.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {mode === "content" ? (
            <>
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 bg-white px-3 py-2 text-xs font-semibold text-on-surface hover:bg-stone-50 disabled:opacity-40"
                disabled={!tenantId || loading || busy}
                onClick={newMessage}
              >
                + New message
              </button>
              <button
                type="button"
                className="rounded-md border border-red-300 bg-white px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50 disabled:opacity-40"
                disabled={!tenantId || loading || busy || !existing}
                onClick={handleDelete}
                title={existing ? "Delete your customised version (revert to system default)" : "No customised version to reset"}
              >
                Reset to default
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-4 py-2 text-xs font-semibold text-on-primary hover:bg-primary/90 disabled:opacity-50"
                disabled={!tenantId || loading || busy}
                onClick={handleSave}
              >
                {busy ? "Saving…" : "Save"}
              </button>
            </>
          ) : (
            <>
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 bg-white px-3 py-2 text-xs font-semibold text-on-surface hover:bg-stone-50 disabled:opacity-40"
                disabled={busy}
                onClick={() => {
                  setMode("content");
                  setError(null);
                }}
              >
                Cancel
              </button>
              {!creating ? (
                <button
                  type="button"
                  className="rounded-md border border-red-300 bg-white px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50 disabled:opacity-40"
                  disabled={busy}
                  onClick={handleDeleteMessage}
                >
                  Delete message
                </button>
              ) : null}
              <button
                type="button"
                className="rounded-md bg-primary px-4 py-2 text-xs font-semibold text-on-primary hover:bg-primary/90 disabled:opacity-50"
                disabled={busy}
                onClick={handleSaveSettings}
              >
                {busy ? "Saving…" : creating ? "Create message" : "Save settings"}
              </button>
            </>
          )}
        </div>
      </div>

      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{error}</div> : null}
      {notice ? <div className="rounded-md border border-green-200 bg-green-50 px-3 py-2 text-xs text-green-700">{notice}</div> : null}

      {mode === "settings" ? (
        <div className="mx-auto max-w-3xl space-y-4 rounded-xl border border-outline-variant/30 bg-surface p-5">
          <div>
            <h2 className="text-sm font-bold text-on-surface">{creating ? "New message" : `Settings — ${entry?.name}`}</h2>
            <p className="text-[11px] text-on-surface-variant">
              Define the message and the personalisation variables operators can insert.
            </p>
          </div>

          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <div>
              <label className="mb-1 block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Name</label>
              <input
                className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs outline-none focus:ring-1 focus:ring-primary"
                value={defName}
                onChange={(e) => setDefName(e.target.value)}
                placeholder="e.g. Order Confirmation"
              />
            </div>
            <div>
              <label className="mb-1 block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Channel</label>
              <select
                className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs outline-none focus:ring-1 focus:ring-primary disabled:opacity-60"
                value={defChannel}
                disabled={!creating}
                onChange={(e) => setDefChannel(e.target.value as TemplateRow["channel"])}
              >
                {(["email", "sms", "print", "admin"] as TemplateRow["channel"][]).map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </div>
            <div className="md:col-span-2">
              <label className="mb-1 block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Description</label>
              <input
                className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs outline-none focus:ring-1 focus:ring-primary"
                value={defDescription}
                onChange={(e) => setDefDescription(e.target.value)}
                placeholder="When is this message sent?"
              />
            </div>
            <div className="md:col-span-2">
              <label className="mb-1 block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
                Key {creating ? <span className="text-green-600">(technical id — cannot change later)</span> : <span className="text-on-surface-variant/60">(fixed)</span>}
              </label>
              <input
                className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs font-mono outline-none focus:ring-1 focus:ring-primary disabled:opacity-60"
                value={defKey}
                disabled={!creating}
                onChange={(e) => setDefKey(e.target.value)}
                placeholder="order.confirmation"
              />
            </div>
          </div>

          <div>
            <div className="mb-1 flex items-center justify-between">
              <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Variables</label>
              <button
                type="button"
                className="rounded border border-outline-variant/30 bg-surface px-2 py-1 text-[11px] font-semibold text-on-surface-variant hover:bg-surface-container-high"
                onClick={addVar}
              >
                + Add variable
              </button>
            </div>
            <div className="overflow-hidden rounded-md border border-outline-variant/20">
              <div className="grid grid-cols-12 gap-2 border-b border-outline-variant/20 bg-surface-container-low px-2 py-1.5 text-[9px] font-bold uppercase tracking-wider text-on-surface-variant">
                <div className="col-span-4">Label (shown in menu)</div>
                <div className="col-span-4">Key (model.path)</div>
                <div className="col-span-3">Sample (preview)</div>
                <div className="col-span-1" />
              </div>
              {defVars.length === 0 ? (
                <div className="px-2 py-3 text-center text-[11px] text-on-surface-variant">No variables. Click “Add variable”.</div>
              ) : (
                defVars.map((v, i) => (
                  <div key={i} className="grid grid-cols-12 items-center gap-2 border-b border-outline-variant/10 px-2 py-1.5 last:border-b-0">
                    <input
                      className="col-span-4 rounded border border-outline-variant/20 bg-surface-container-lowest px-2 py-1 text-[11px] outline-none focus:ring-1 focus:ring-primary"
                      value={v.label}
                      onChange={(e) => patchVar(i, { label: e.target.value })}
                      placeholder="Order number"
                    />
                    <input
                      className="col-span-4 rounded border border-outline-variant/20 bg-surface-container-lowest px-2 py-1 font-mono text-[11px] outline-none focus:ring-1 focus:ring-primary"
                      value={v.path}
                      onChange={(e) => patchVar(i, { path: e.target.value })}
                      placeholder="orderId"
                    />
                    <input
                      className="col-span-3 rounded border border-outline-variant/20 bg-surface-container-lowest px-2 py-1 text-[11px] outline-none focus:ring-1 focus:ring-primary"
                      value={String(v.sample ?? "")}
                      onChange={(e) => patchVar(i, { sample: e.target.value })}
                      placeholder="SO-100428"
                    />
                    <button
                      type="button"
                      className="col-span-1 justify-self-center rounded p-1 text-red-600 hover:bg-red-50"
                      title="Remove variable"
                      onClick={() => removeVar(i)}
                    >
                      ✕
                    </button>
                  </div>
                ))
              )}
            </div>
            <p className="mt-1 text-[10px] text-on-surface-variant">
              “Key” is the Scriban path — a variable with key <span className="font-mono">orderId</span> is inserted as{" "}
              <span className="font-mono">{"{{ model.orderId }}"}</span>.
            </p>
          </div>
        </div>
      ) : (
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-12">
        {/* ── Template catalog ── */}
        <aside className="lg:col-span-3">
          <div className="rounded-xl border border-outline-variant/30 bg-surface">
            <div className="border-b border-outline-variant/20 px-3 py-2 text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
              Messages
            </div>
            <ul className="divide-y divide-outline-variant/15">
              {catalog.map((c) => {
                const customised = rows.some((r) => r.key === c.key && r.channel === c.channel);
                const active = c.id === entry?.id;
                return (
                  <li key={c.id}>
                    <button
                      type="button"
                      onClick={() => selectTemplate(c.id)}
                      className={`block w-full px-3 py-2.5 text-left transition-colors ${active ? "bg-primary/10" : "hover:bg-stone-50"}`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="truncate text-xs font-semibold text-on-surface">{c.name}</span>
                        {customised ? (
                          <span className="shrink-0 rounded-full bg-green-100 px-1.5 py-0.5 text-[9px] font-bold uppercase text-green-700">
                            edited
                          </span>
                        ) : null}
                      </div>
                      <p className="mt-0.5 line-clamp-2 text-[10px] leading-snug text-on-surface-variant">{c.description}</p>
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>
        </aside>

        {/* ── Editor ── */}
        <div className="space-y-3 lg:col-span-5">
          <div>
            <div className="flex items-center justify-between gap-2">
              <div className="flex items-center gap-1.5">
                <h2 className="text-sm font-bold text-on-surface">{entry?.name}</h2>
                <button
                  type="button"
                  className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
                  title="Edit message settings (name, description, variables)"
                  onClick={openSettings}
                >
                  <svg viewBox="0 0 24 24" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="2">
                    <circle cx="12" cy="12" r="3" />
                    <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
                  </svg>
                </button>
              </div>
              {/* Language tabs */}
              <div className="flex items-center gap-1">
                {tabLanguages.map((l) => {
                  const isActive = l.isoCode === resolvedIso;
                  const hasContent = savedLocales.has(l.isoCode);
                  return (
                    <button
                      key={l.isoCode}
                      type="button"
                      title={l.name}
                      onClick={() => setActiveIso(l.isoCode)}
                      className={`flex items-center gap-1 rounded px-2 py-1 text-[10px] font-bold uppercase tracking-wide transition-colors ${
                        isActive ? "bg-primary text-on-primary" : "bg-surface-container text-on-surface-variant hover:bg-surface-container-high"
                      }`}
                    >
                      {langPill(l.isoCode)}
                      {hasContent && !isActive ? <span className="h-1 w-1 rounded-full bg-primary/70" /> : null}
                    </button>
                  );
                })}
              </div>
            </div>
            <p className="mt-0.5 text-[11px] text-on-surface-variant">{entry?.description}</p>
          </div>

          <div>
            <div className="mb-1 flex items-center justify-between">
              <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Subject line</label>
              <VariableMenu variables={entry?.variables ?? []} onPick={insertSubjectVar} />
            </div>
            <input
              ref={subjectRef}
              className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs outline-none focus:ring-1 focus:ring-primary"
              value={draftSubject}
              onChange={(e) => setDraftSubject(e.target.value)}
              placeholder="e.g. Your order is confirmed"
            />
          </div>

          <div>
            <label className="mb-1 block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Email content</label>
            <RichBodyEditor
              key={`${entry?.id}|${resolvedIso}|${editorKey}`}
              initialHtml={draftBody}
              variables={entry?.variables ?? []}
              onChange={setDraftBody}
            />
            <p className="mt-1 text-[10px] text-on-surface-variant">
              Tip: use <b>Insert variable</b> to drop in customer details. They’re filled in automatically when the email is sent.
            </p>
          </div>
        </div>

        {/* ── Live preview ── */}
        <div className="lg:col-span-4">
          <div className="sticky top-2 rounded-xl border border-outline-variant/30 bg-stone-50 p-3">
            <div className="mb-2 flex items-center justify-between">
              <span className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Live preview</span>
              <span className="rounded-full bg-stone-200 px-2 py-0.5 text-[9px] font-semibold text-on-surface-variant">sample data</span>
            </div>
            <div className="overflow-hidden rounded-lg border border-outline-variant/20 bg-white shadow-sm">
              <div className="border-b border-outline-variant/15 px-3 py-2">
                <div className="text-[9px] uppercase tracking-wider text-on-surface-variant">Subject</div>
                <div className="truncate text-xs font-semibold text-on-surface">{previewSubject || draftSubject || <span className="text-on-surface-variant">(no subject)</span>}</div>
              </div>
              <div className="min-h-[260px] px-4 py-3">
                {previewErr ? (
                  <div className="rounded border border-amber-200 bg-amber-50 px-2 py-1 text-[11px] text-amber-800">
                    Template error: {previewErr}
                  </div>
                ) : previewBody ? (
                  // preview is trusted admin-only content rendered with sample data
                  <div className="prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: previewBody }} />
                ) : (
                  <div className="text-xs italic text-on-surface-variant">Start typing to see a preview…</div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
      )}
    </section>
  );
}
