/**
 * Brand/category image upload (DAI Phase 1).
 * Posts a multipart file to dCMS.Web (NOT the gateway) and returns a public URL string
 * (e.g. "/media/brands/t1/ab12….png") to store on the entity instead of a base64 blob.
 * Auth: relies on the Umbraco backoffice cookie (same-origin).
 */

const UPLOAD_URL = "/umbraco/dcms/api/media/upload";

type UploadResponse = { data: { url: string } | null; meta: unknown; error: { message?: string } | null };

/** Upload an image file; resolves to its served URL. Throws on validation/HTTP errors. */
export async function uploadImage(file: File, folder = "brands"): Promise<string> {
  const form = new FormData();
  form.append("file", file);
  form.append("folder", folder);

  // NB: do NOT set Content-Type — the browser sets the multipart boundary.
  const res = await fetch(UPLOAD_URL, {
    method: "POST",
    credentials: "same-origin",
    body: form,
  });

  if (!res.ok) {
    let msg = `Upload failed (HTTP ${res.status})`;
    try {
      const body = (await res.json()) as UploadResponse;
      if (body?.error?.message) msg = body.error.message;
    } catch {
      /* ignore parse error */
    }
    throw new Error(msg);
  }

  const body = (await res.json()) as UploadResponse;
  const url = body.data?.url;
  if (!url) throw new Error("Upload succeeded but no URL was returned.");
  return url;
}
