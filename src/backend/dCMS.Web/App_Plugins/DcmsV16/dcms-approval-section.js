import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

const _v = typeof __BUILD_VERSION__ !== "undefined" ? __BUILD_VERSION__ : Date.now();
const SPA_JS = `/App_Plugins/DcmsV16/dist/approval-spa.js?v=${_v}`;
const SPA_CSS = `/App_Plugins/DcmsV16/dist/approval-spa.css?v=${_v}`;

function resolveUrl(path) {
  try {
    return new URL(path, window.location.origin).toString();
  } catch {
    return path;
  }
}

export default class DcmsApprovalSectionElement extends UmbElementMixin(HTMLElement) {
  /** @type {HTMLElement | null} */
  #host = null;
  /** @type {{ mount?: (el: HTMLElement) => void, unmount?: (el: HTMLElement) => void } | null} */
  #spa = null;

  constructor() {
    super();
  }

  async connectedCallback() {
    this.style.display = "block";
    this.style.position = "relative";
    this.style.width = "100%";
    this.style.height = "100%";

    this.innerHTML = `<div data-react-root style="position:absolute;inset:0"></div>`;
    this.#host = this.querySelector("[data-react-root]");
    if (!this.#host) return;

    try {
      const cssUrl = resolveUrl(SPA_CSS);
      const cssRes = await fetch(cssUrl, { method: "GET", credentials: "same-origin" });
      if (!cssRes.ok) throw new Error(`GET ${cssUrl} -> ${cssRes.status} ${cssRes.statusText}`);
      const cssText = await cssRes.text();

      const style = document.createElement("style");
      style.id = "dcms-approval-spa-style";
      style.textContent = cssText;
      this.insertBefore(style, this.#host);

      const jsUrl = resolveUrl(SPA_JS);
      const jsRes = await fetch(jsUrl, { method: "GET", credentials: "same-origin" });
      if (!jsRes.ok) throw new Error(`GET ${jsUrl} -> ${jsRes.status} ${jsRes.statusText}`);

      this.#spa = await import(/* @vite-ignore */ SPA_JS);
      this.#spa?.mount?.(this.#host);
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      if (this.#host) {
        this.#host.innerHTML =
          `<div style="padding:16px 20px;font-family:system-ui,sans-serif;color:#b91c1c">
            <div style="font-weight:600;margin-bottom:6px">Could not load Approval SPA bundle.</div>
            <div style="margin-bottom:8px">Expected: <code>${SPA_JS}</code></div>
            <div style="background:#fff;border:1px solid #e9e9eb;border-radius:6px;padding:10px 12px;color:#0f172a">
              <div style="font-size:12px;color:#64748b;margin-bottom:4px">Error</div>
              <code style="white-space:pre-wrap;word-break:break-word">${msg}</code>
            </div>
            <div style="margin-top:10px;font-size:12px;color:#64748b">
              Run <code>npm run build:approval</code> in <code>src/backoffice/dcms-backoffice-spa</code> and restart Umbraco.
            </div>
          </div>`;
      }
      // eslint-disable-next-line no-console
      console.error("[dcms-approval-section]", e);
    }
  }

  disconnectedCallback() {
    if (this.#host) this.#spa?.unmount?.(this.#host);
    this.#host = null;
  }
}

customElements.define("dcms-approval-section", DcmsApprovalSectionElement);
