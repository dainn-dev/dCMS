import ExcelJS from "exceljs";

/** Spec column headers for product import (matches former CSV template). */
export const PRODUCT_IMPORT_TEMPLATE_HEADERS = [
  "UPC",
  "SKU",
  "PID1",
  "PID2",
  "Product Name",
  "Category",
  "Brand",
  "Retail Price",
  "Current Price",
  "Qty",
  "eStore Qty",
  "eStore CutOff Qty",
  "Item Type",
  "Enable Collection",
  "Enable Delivery",
  "Enable Overseas Delivery",
  "Inventory Sync",
  "Delivery Start Date",
  "Delivery Lead Time",
  "Handling Fee",
  "ATTRIB_COLOR",
  "ATTRIB_SIZE",
  "ATTRIB_STYLE",
  "Meta Title",
  "Meta Keywords",
  "Meta Description",
  "Product URL",
] as const;

export const INVENTORY_IMPORT_TEMPLATE_HEADERS = [
  "UPC",
  "Product Name",
  "Store ID",
  "Store Quantity",
  "Replace Quantity",
] as const;

async function writeHeaderOnlyXlsx(sheetName: string, headers: readonly string[], filename: string) {
  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet(sheetName, {
    views: [{ state: "frozen", ySplit: 1 }],
  });
  sheet.addRow([...headers]);
  sheet.getRow(1).font = { bold: true };
  headers.forEach((_, i) => {
    sheet.getColumn(i + 1).width = Math.min(Math.max(String(headers[i]).length + 2, 12), 40);
  });

  const buf = await workbook.xlsx.writeBuffer();
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

export async function downloadProductImportTemplateXlsx() {
  await writeHeaderOnlyXlsx("ProductImport", PRODUCT_IMPORT_TEMPLATE_HEADERS, "product-import-template.xlsx");
}

export async function downloadInventoryImportTemplateXlsx() {
  await writeHeaderOnlyXlsx(
    "InventoryImport",
    INVENTORY_IMPORT_TEMPLATE_HEADERS,
    "inventory-import-template.xlsx"
  );
}
