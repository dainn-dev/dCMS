import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsAccordionBlockView extends DcmsBlockBase {
  static properties = {
    ...DcmsBlockBase.properties,
    _openIdx: { state: true },
  };

  constructor() {
    super();
    this._openIdx = 0;
  }

  render() {
    const c = this.content || {};
    const items = c.items?.contentData || [];
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const disableCounter = c.disableCounter == 1;

    return html`
      <div class="wrap" style=${schemeStyle}>
        ${items.length === 0
          ? html`<p class="empty">(no accordion items)</p>`
          : items.map((item, i) => html`
              <div class="acc-item">
                <div class="acc-header" @click=${() => this._openIdx = this._openIdx === i ? -1 : i}>
                  ${!disableCounter ? html`<span class="counter">${String(i + 1).padStart(2, "0")}</span>` : ""}
                  <span class="title">${item.headline || `Item ${i + 1}`}</span>
                  <span class="arrow">${this._openIdx === i ? "▲" : "▼"}</span>
                </div>
                ${this._openIdx === i && item.richText?.markup ? html`
                  <div class="acc-body" .innerHTML=${item.richText.markup}></div>
                ` : ""}
              </div>
            `)}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 10px 12px; }
    .acc-item { border-bottom: 1px solid var(--uui-color-border,#e3e3e3); }
    .acc-header {
      display: flex; align-items: center; gap: 8px; padding: 8px 0;
      cursor: pointer; user-select: none;
    }
    .counter { font-size: 11px; font-weight: 700; color: var(--uui-color-text-alt,#767676); min-width: 22px; }
    .title { flex: 1; font-size: 13px; font-weight: 600; }
    .arrow { font-size: 10px; color: var(--uui-color-text-alt,#767676); }
    .acc-body { font-size: 12px; line-height: 1.6; padding: 6px 0 10px 30px; color: var(--uui-color-text,#333); }
    .acc-body p { margin: 0.2em 0; }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin: 0; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 8px; }
  `];
}

customElements.define("dcms-accordion-block-view", DcmsAccordionBlockView);
