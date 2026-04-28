type Props = { tenantId?: string; storeId?: string; authToken?: string };

/**
 * DAI-711: delivery slot utilization report.
 * Backend endpoint is not yet implemented — this page is a placeholder so that
 * navigation works while the data pipeline is being built. Replace with a
 * <ReportView /> instance once the API is ready.
 */
export function DeliverySlotReportPage(_: Props) {
  return (
    <div
      className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low"
      aria-label="Delivery slots report"
    >
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Delivery slots</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Delivery slots</h1>
          <p className="max-w-2xl text-sm text-on-surface-variant">
            Utilization per delivery slot (booked vs. capacity).
          </p>
        </div>
      </header>

      <div className="flex flex-1 items-center justify-center p-6">
        <div className="w-full max-w-md rounded-2xl border border-dashed border-outline-variant/30 bg-surface-container-lowest px-6 py-10 text-center">
          <p className="text-xs font-bold uppercase tracking-widest text-primary">Coming soon</p>
          <h2 className="mt-3 font-headline text-lg font-bold text-on-surface">No data yet</h2>
          <p className="mt-2 text-xs text-on-surface-variant">
            The delivery-slot analytics pipeline is being built. This report will surface booked vs.
            capacity per slot once the backend endpoint ships.
          </p>
        </div>
      </div>
    </div>
  );
}
