import ExcelJS from "exceljs";

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

export async function exportReportRowsToXlsx(sheetName: string, filename: string, headers: string[], rows: string[][]) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet(sheetName.slice(0, 31) || "Report", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  sheet.addRow(headers);
  sheet.getRow(1).font = { bold: true };
  rows.forEach((r) => sheet.addRow(r));
  headers.forEach((h, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(h).length + 2, 10), 36);
  });
  const buf = await workbook.xlsx.writeBuffer();
  await triggerDownload(buf, filename.endsWith(".xlsx") ? filename : `${filename}.xlsx`);
}
