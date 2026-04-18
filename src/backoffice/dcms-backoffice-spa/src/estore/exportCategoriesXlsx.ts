import ExcelJS from "exceljs";

/** Minimal tree shape for export (matches CategoriesPage CatNode). */
export type CategoryExportNode = {
  id: string;
  name: string;
  active?: boolean;
  publishFrom?: string;
  publishUntil?: string;
  sortOrder?: number;
  children?: CategoryExportNode[];
};

const TODAY = new Date();
TODAY.setHours(0, 0, 0, 0);

function isExpired(node: CategoryExportNode): boolean {
  if (!node.publishUntil) return false;
  return new Date(node.publishUntil) < TODAY;
}

export type CategoryExportRow = {
  categoryId: string;
  categoryName: string;
  parentCategory: string;
  sortOrder: string;
  status: string;
  expired: string;
  publishFrom: string;
  publishUntil: string;
};

export function flattenCategoriesForExport(
  nodes: CategoryExportNode[],
  parentName = "(Root)"
): CategoryExportRow[] {
  return nodes.flatMap((n) => {
    const row: CategoryExportRow = {
      categoryId: n.id,
      categoryName: n.name,
      parentCategory: parentName,
      sortOrder: String(n.sortOrder ?? ""),
      status: n.active !== false ? "Active" : "Inactive",
      expired: isExpired(n) ? "Yes" : "No",
      publishFrom: n.publishFrom ?? "",
      publishUntil: n.publishUntil ?? "",
    };
    return [row, ...flattenCategoriesForExport(n.children ?? [], n.name)];
  });
}

/** CR §4.3.5 — real .xlsx export. */
export async function exportCategoriesToXlsx(nodes: CategoryExportNode[]) {
  const rows = flattenCategoriesForExport(nodes);
  if (rows.length === 0) return;

  const workbook = new ExcelJS.Workbook();
  const sheet = workbook.addWorksheet("Categories", {
    views: [{ state: "frozen", ySplit: 1 }],
  });

  sheet.addRow([
    "Category ID",
    "Category Name",
    "Parent Category",
    "Sort Order",
    "Status",
    "Expired",
    "Publish From",
    "Publish Until",
  ]);
  sheet.getRow(1).font = { bold: true };

  for (const r of rows) {
    sheet.addRow([
      r.categoryId,
      r.categoryName,
      r.parentCategory,
      r.sortOrder,
      r.status,
      r.expired,
      r.publishFrom,
      r.publishUntil,
    ]);
  }

  sheet.getColumn(1).width = 12;
  sheet.getColumn(2).width = 28;
  sheet.getColumn(3).width = 22;
  sheet.getColumn(4).width = 10;
  sheet.getColumn(5).width = 10;
  sheet.getColumn(6).width = 10;
  sheet.getColumn(7).width = 18;
  sheet.getColumn(8).width = 18;

  const buf = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buf], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `category-export-${new Date().toISOString().slice(0, 10)}.xlsx`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
