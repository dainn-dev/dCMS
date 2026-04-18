import ExcelJS from "exceljs";
import type { AttributeListRow } from "./attributes-columns";

export const ATTR_VALUES_IMPORT_HEADERS = [
  "Attribute Name",
  "Attribute Code",
  "Values (semicolon-separated)",
] as const;

const SAMPLE_ROWS: [string, string, string][] = [
  ["Material Composition", "mat_composition", "Cotton;Polyester;Silk;Wool"],
  ["Primary Color", "color_primary", "Red;Blue;Green;Black;White"],
  ["Country of Origin", "geo_origin", "Malaysia;Singapore;Thailand;Indonesia"],
];

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

export async function downloadAttributeValuesImportTemplateXlsx() {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("Template", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  sheet.addRow([...ATTR_VALUES_IMPORT_HEADERS]);
  sheet.getRow(1).font = { bold: true };
  SAMPLE_ROWS.forEach((r) => sheet.addRow(r));
  ATTR_VALUES_IMPORT_HEADERS.forEach((h, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(h).length + 4, 18), 48);
  });
  const buf = await workbook.xlsx.writeBuffer();
  await triggerDownload(buf, "attribute-values-import-template.xlsx");
}

export async function exportAttributesSchemaXlsx(rows: AttributeListRow[]) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("Attributes", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  const headers = ["Seq.", "Name", "Code", "Type", "Required"] as const;
  sheet.addRow([...headers]);
  sheet.getRow(1).font = { bold: true };
  rows.forEach((r) => {
    sheet.addRow([r.seq, r.name, r.code, r.type, r.required ? "Yes" : "No"]);
  });
  headers.forEach((h, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(h).length + 2, 12), 36);
  });
  const buf = await workbook.xlsx.writeBuffer();
  await triggerDownload(buf, "attributes-schema-export.xlsx");
}

export async function exportSingleAttributeValuesXlsx(
  attrName: string,
  attrCode: string,
  valueRows: { name: string; value: string }[]
) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("Values", {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  const headers = ["Attribute Name", "Attribute Code", "Value Name", "Value"] as const;
  sheet.addRow([...headers]);
  sheet.getRow(1).font = { bold: true };
  valueRows.forEach((v) => {
    sheet.addRow([attrName, attrCode, v.name, v.value]);
  });
  headers.forEach((h, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(h).length + 2, 14), 56);
  });
  const buf = await workbook.xlsx.writeBuffer();
  const safe = (attrCode || "attribute").replace(/[^\w.-]+/g, "_");
  await triggerDownload(buf, `${safe}-values-export.xlsx`);
}
