import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsIconButtonsBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const buttons = c.buttons?.contentData || [];
    const align = c.horizontalAlignment?.toLowerCase() || "left";
    const justifyCss = align === "right" ? "flex-end" : align === "center" ? "center" : "flex-start";

    return html`
      <div class="wrap" style="justify-content:${justifyCss}">
        ${buttons.length
          ? buttons.map((btn, i) => html`
              <div class="icon-btn" title="${Array.isArray(btn.link) ? (btn.link[0]?.name || "") : ""}">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                  <circle cx="12" cy="12" r="10"/>
                  <line x1="12" y1="8" x2="12" y2="16"/>
                  <line x1="8" y1="12" x2="16" y2="12"/>
                </svg>
              </div>
            `)
          : html`<div class="empty">(no icon buttons)</div>`}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-wrap: wrap; gap: 8px; padding: 12px; align-items: center; }
    .icon-btn {
      width: 40px; height: 40px; border-radius: 50%;
      border: 1px solid var(--uui-color-border,#e3e3e3); background: #fff;
      display: flex; align-items: center; justify-content: center;
      color: var(--uui-color-text,#333);
    }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; width: 100%; }
  `];
}

customElements.define("dcms-icon-buttons-block-view", DcmsIconButtonsBlockView);
