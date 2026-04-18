import ExcelJS from "exceljs";
import type { PromoListRow } from "./promotions-columns";

const PROMO_EXPORT_HEADERS = [
  "Promo Type",
  "Discount",
  "Promo Value",
  "Minimum Spend",
  "Code",
  "Start Date / Time",
  "End Date / Time",
  "Redemption Limit",
  "Used",
  "% Used",
] as const;

async function triggerDownload(buf: ArrayBuffer, filename: string) {
  const blob = new Blob([buf], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export async function exportPromoCodesToXlsx(rows: PromoListRow[]) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("PromoCodes", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  sheet.addRow([...PROMO_EXPORT_HEADERS]);
  sheet.getRow(1).font = { bold: true };

  const redemptionLimit = 500;
  rows.forEach((r) => {
    const used = Math.round((r.usedPct / 100) * redemptionLimit);
    sheet.addRow([
      r.promoType,
      r.discount,
      r.value,
      r.minSpend,
      r.code,
      r.scheduleStart,
      r.scheduleEnd.replace(/^to\s*/, ""),
      redemptionLimit,
      used,
      `${r.usedPct}%`,
    ]);
  });

  PROMO_EXPORT_HEADERS.forEach((h, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(h).length + 2, 12), 28);
  });

  const buf = await workbook.xlsx.writeBuffer();
  await triggerDownload(buf, "promo-codes-export.xlsx");
}

export type GroupedChildExportRow = {
  code: string;
  status: string;
  startDate: string;
  endDate: string;
  used: number;
  limit: number;
};

export async function exportGroupedPromoCodesToXlsx(parentCode: string, children: GroupedChildExportRow[]) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("GroupedCodes", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  const headers = ["Parent Code", "Code", "Status", "Start", "End", "Used", "Limit"] as const;
  sheet.addRow([...headers]);
  sheet.getRow(1).font = { bold: true };
  children.forEach((c) => {
    sheet.addRow([parentCode, c.code, c.status, c.startDate, c.endDate, c.used, c.limit]);
  });
  headers.forEach((h, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(h).length + 2, 10), 24);
  });
  const safe = parentCode.replace(/[^\w.-]+/g, "_");
  const buf = await workbook.xlsx.writeBuffer();
  await triggerDownload(buf, `${safe}-grouped-promo-codes.xlsx`);
}
