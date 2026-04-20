import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsButtonsGroupBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const buttons = c.buttons?.contentData || [];
    const align = c.horizontalAlignment?.toLowerCase() || "left";
    const justifyCss = align === "right" ? "flex-end" : align === "center" ? "center" : "flex-start";

    return html`
      <div class="wrap" style="${schemeStyle}justify-content:${justifyCss}">
        ${buttons.length
          ? buttons.map(btn => {
              const name = Array.isArray(btn.link) ? (btn.link[0]?.name || btn.link[0]?.url || "Button") : "Button";
              const variant = btn.variant || "Primary";
              const size = btn.size || "Normal";
              return html`
                <div class="btn-preview ${variant === "Secondary" ? "sec" : "pri"} ${size === "Large" ? "lg" : size === "Small" ? "sm" : ""}">
                  ${name}
                </div>
              `;
            })
          : html`<div class="btn-preview pri">(buttons)</div>`}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-wrap: wrap; gap: 8px; padding: 12px; align-items: center; }
    .btn-preview {
      display: inline-block; border-radius: 4px; font-weight: 600; cursor: default;
    }
    .btn-preview.pri { background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff); }
    .btn-preview.sec { background: transparent; border: 1.5px solid var(--block-btn-border,#2986cc); color: var(--block-btn-border,#2986cc); }
    .btn-preview.lg { padding: 8px 20px; font-size: 15px; }
    .btn-preview.sm { padding: 3px 8px; font-size: 11px; }
    .btn-preview:not(.lg):not(.sm) { padding: 5px 14px; font-size: 13px; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; width: 100%; }
  `];
}

customElements.define("dcms-buttons-group-block-view", DcmsButtonsGroupBlockView);
