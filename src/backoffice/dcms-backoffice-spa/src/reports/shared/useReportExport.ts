import { useMemo } from "react";

/**
 * Shared rules for report Export to Excel (DAI-470 / spec 7.0).
 */
export function useReportExportState(loading: boolean, rowCount: number) {
  return useMemo(
    () => ({
      exportDisabled: loading || rowCount === 0,
    }),
    [loading, rowCount]
  );
}
