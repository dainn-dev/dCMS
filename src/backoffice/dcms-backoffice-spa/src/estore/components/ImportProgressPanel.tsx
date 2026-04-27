import { useEffect, useRef, useState } from "react";
import { getImportJob, isTerminal, type ImportJob } from "../api/importsApi";

type Props = {
  tenantId: string;
  jobId: string;
  pollIntervalMs?: number;
  onTerminal?: (job: ImportJob) => void;
};

const POLL_DEFAULT = 5000;

const labelChip =
  "inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[0.625rem] font-bold uppercase tracking-wider";

const statusTone: Record<ImportJob["status"], string> = {
  Pending: "bg-surface-container text-on-surface-variant",
  Running: "bg-primary/10 text-primary",
  Completed: "bg-emerald-100 text-emerald-700",
  PartiallyCompleted: "bg-amber-100 text-amber-700",
  Failed: "bg-rose-100 text-rose-700",
};

export function ImportProgressPanel({ tenantId, jobId, pollIntervalMs = POLL_DEFAULT, onTerminal }: Props) {
  const [job, setJob] = useState<ImportJob | null>(null);
  const [error, setError] = useState<string | null>(null);
  const stoppedRef = useRef(false);

  useEffect(() => {
    stoppedRef.current = false;
    const controller = new AbortController();

    const tick = async () => {
      if (stoppedRef.current) return;
      try {
        const j = await getImportJob(tenantId, jobId, controller.signal);
        setJob(j);
        setError(null);
        if (isTerminal(j.status)) {
          stoppedRef.current = true;
          onTerminal?.(j);
          return;
        }
      } catch (e: any) {
        if (e?.name === "AbortError") return;
        setError(e?.message ?? "Failed to fetch job status");
      }
      if (!stoppedRef.current) window.setTimeout(tick, pollIntervalMs);
    };

    tick();
    return () => {
      stoppedRef.current = true;
      controller.abort();
    };
  }, [tenantId, jobId, pollIntervalMs, onTerminal]);

  if (error) {
    return <div className="text-xs text-rose-600">Error: {error}</div>;
  }
  if (!job) {
    return <div className="text-xs text-on-surface-variant">Loading job status…</div>;
  }

  const total = job.total ?? 0;
  const pct = total > 0 ? Math.min(100, Math.round((job.processed / total) * 100)) : 0;

  return (
    <div className="flex flex-col gap-3 rounded-md border border-outline-variant/20 bg-surface-container-lowest p-4">
      <div className="flex items-center justify-between">
        <div className="text-xs font-mono text-on-surface-variant">{job.id}</div>
        <span className={`${labelChip} ${statusTone[job.status]}`}>{job.status}</span>
      </div>
      <div className="text-xs text-on-surface-variant">
        Processed <span className="font-bold text-on-surface">{job.processed}</span>
        {total > 0 ? ` / ${total} (${pct}%)` : ""}
      </div>
      {total > 0 && (
        <div className="h-2 w-full overflow-hidden rounded-full bg-surface-container">
          <div
            className="h-full bg-primary transition-all"
            style={{ width: `${pct}%` }}
          />
        </div>
      )}
      {job.errors.length > 0 && (
        <div className="flex flex-col gap-1">
          <div className="text-[0.625rem] font-bold uppercase tracking-wider text-rose-700">
            Row errors ({job.errors.length})
          </div>
          <ul className="max-h-48 overflow-auto rounded border border-rose-200 bg-rose-50 p-2 text-[0.6875rem] text-rose-700">
            {job.errors.slice(0, 100).map((e, i) => (
              <li key={`${e.rowIndex}-${i}`} className="font-mono">
                row {e.rowIndex} ({e.key}): {e.message}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
