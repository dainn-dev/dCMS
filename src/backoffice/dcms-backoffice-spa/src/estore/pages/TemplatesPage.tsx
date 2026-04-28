import { useEffect, useMemo, useState } from "react";
import { listTemplates, previewTemplate, putTemplate, type TemplateRow } from "../api/templatesApi";
import { inputClass } from "../../reports/shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

const CHANNELS: TemplateRow["channel"][] = ["email", "admin", "print", "sms"];

export function TemplatesPage({ tenantId, storeId, authToken }: Props) {
  const [rows, setRows] = useState<TemplateRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [key, setKey] = useState("order.confirmation");
  const [locale, setLocale] = useState("en-US");
  const [channel, setChannel] = useState<TemplateRow["channel"]>("email");
  const [subject, setSubject] = useState("Your order is confirmed");
  const [body, setBody] = useState("<h1>Hello</h1>");
  const [preview, setPreview] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const existing = useMemo(
    () => rows.find((r) => r.key === key && r.locale === locale && r.channel === channel && r.tenantId === (tenantId ?? null)),
    [rows, key, locale, channel, tenantId],
  );

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

  useEffect(() => {
    if (!existing) return;
    setSubject(existing.subject ?? "");
    setBody(existing.body ?? "");
  }, [existing?.id]);

  async function handlePreview() {
    if (!tenantId) return;
    const res = await previewTemplate(
      tenantId,
      storeId,
      { key, locale, channel, subject, body, sampleModel: { orderId: "demo", total: 123.45 } },
      authToken,
    );
    setPreview(typeof res.body === "string" ? res.body : JSON.stringify(res.body, null, 2));
  }

  async function handleSave() {
    if (!tenantId) return;
    await putTemplate(
      tenantId,
      storeId,
      { id: existing?.id, key, locale, channel, subject, body, modelVersion: existing?.modelVersion ?? 1 },
      authToken,
    );
    const data = await listTemplates(tenantId, storeId, authToken);
    setRows(data);
  }

  return (
    <section className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Templates</h1>
          <p className="text-xs text-on-surface-variant">Tenant-scoped templates with locale variants (DAI-687).</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="rounded-md bg-stone-200 px-3 py-2 text-xs font-semibold hover:bg-stone-300 disabled:opacity-50"
            disabled={!tenantId || loading}
            onClick={handlePreview}
          >
            Preview
          </button>
          <button
            type="button"
            className="rounded-md bg-primary px-3 py-2 text-xs font-semibold text-on-primary hover:bg-primary/90 disabled:opacity-50"
            disabled={!tenantId || loading}
            onClick={handleSave}
          >
            Save
          </button>
        </div>
      </div>

      {error ? <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{error}</div> : null}

      <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
        <div>
          <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Key</label>
          <input className={inputClass} value={key} onChange={(e) => setKey(e.target.value)} />
        </div>
        <div>
          <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Locale</label>
          <input className={inputClass} value={locale} onChange={(e) => setLocale(e.target.value)} />
        </div>
        <div>
          <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Channel</label>
          <select className={inputClass} value={channel} onChange={(e) => setChannel(e.target.value as TemplateRow["channel"])}>
            {CHANNELS.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        <div className="space-y-3">
          <div>
            <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Subject</label>
            <input className={inputClass} value={subject} onChange={(e) => setSubject(e.target.value)} />
          </div>
          <div>
            <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Body (HTML/Scriban)</label>
            <textarea
              className={inputClass}
              style={{ minHeight: 260, fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace" }}
              value={body}
              onChange={(e) => setBody(e.target.value)}
            />
          </div>
        </div>

        <div className="space-y-3">
          <div className="rounded-xl border border-outline-variant/30 bg-surface p-3">
            <div className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-2">Preview</div>
            <div
              className="prose prose-sm max-w-none"
              // preview is trusted admin-only content
              dangerouslySetInnerHTML={{ __html: preview || "<em>Click Preview</em>" }}
            />
          </div>
        </div>
      </div>
    </section>
  );
}

