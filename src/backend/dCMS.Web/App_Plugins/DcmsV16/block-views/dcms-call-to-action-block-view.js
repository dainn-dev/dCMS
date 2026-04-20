import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsCallToActionBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const linkName = Array.isArray(c.link) ? (c.link[0]?.name || c.link[0]?.url || "(button)") : "(button)";
    const variant = c.variant || "Primary";
    const size = c.size || "Normal";
    const align = c.horizontalAlignment?.toLowerCase() || "left";
    const justifyCss = align === "right" ? "flex-end" : align === "center" ? "center" : "flex-start";

    return html`
      <div class="wrap" style="${schemeStyle}justify-content:${justifyCss}">
        <div class="btn-preview ${variant === "Secondary" ? "sec" : "pri"} ${size === "Large" ? "lg" : size === "Small" ? "sm" : ""}">
          ${linkName}
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; padding: 12px; }
    .btn-preview {
      display: inline-block; border-radius: 4px; font-weight: 600; cursor: default;
    }
    .btn-preview.pri { background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff); }
    .btn-preview.sec { background: transparent; border: 1.5px solid var(--block-btn-border,#2986cc); color: var(--block-btn-border,#2986cc); }
    .btn-preview.lg { padding: 8px 20px; font-size: 15px; }
    .btn-preview.sm { padding: 3px 8px; font-size: 11px; }
    .btn-preview:not(.lg):not(.sm) { padding: 5px 14px; font-size: 13px; }
  `];
}

customElements.define("dcms-call-to-action-block-view", DcmsCallToActionBlockView);
