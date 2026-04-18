import ExcelJS from "exceljs";
import type { BrandListRow } from "./brands-columns";

/** Builds a real .xlsx (CR §4.1.3 / DAI-400): Brand Code, Brand Name, Image (URL), Status. */
export async function exportBrandsToXlsx(rows: BrandListRow[]) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("Brands", {
    views: [{ state: "frozen", ySplit: 1 }],
  });

  sheet.addRow(["Brand Code", "Brand Name", "Image (URL)", "Status"]);
  const header = sheet.getRow(1);
  header.font = { bold: true };
  header.alignment = { vertical: "middle" };

  for (const r of rows) {
    sheet.addRow([r.code, r.name, r.imageSrc ?? "", r.active ? "Active" : "Inactive"]);
  }

  sheet.getColumn(1).width = 14;
  sheet.getColumn(2).width = 32;
  sheet.getColumn(3).width = 48;
  sheet.getColumn(4).width = 12;

  const buf = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buf], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `brand-export-${new Date().toISOString().slice(0, 10)}.xlsx`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
