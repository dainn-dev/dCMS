import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsFeaturesWithDescriptionsBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h2");
    const title = c.title || c.headlineText || c.headline || "";
    const items = c.items?.contentData || [];

    return html`
      <div class="wrap" style=${schemeStyle}>
        ${title ? html`<div class="title ${tag}">${title}</div>` : ""}
        <div class="layout">
          <div class="accordion">
            ${items.length
              ? items.map((item, i) => html`
                  <div class="acc-item ${i === 0 ? "open" : ""}">
                    <div class="acc-header">
                      <span class="acc-title">${item.headline || `Item ${i+1}`}</span>
                      <span class="acc-arrow">${i === 0 ? "▲" : "▼"}</span>
                    </div>
                    ${i === 0 && item.richText?.markup ? html`
                      <div class="acc-body" .innerHTML=${item.richText.markup.slice(0,150)}></div>
                    ` : ""}
                  </div>
                `)
              : html`<p class="empty">(no items)</p>`}
          </div>
          <div class="img-col">
            <div class="img-box">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <rect x="3" y="3" width="18" height="18" rx="2"/>
                <circle cx="8.5" cy="8.5" r="1.5"/>
                <polyline points="21 15 16 10 5 21"/>
              </svg>
            </div>
          </div>
        </div>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; }
    .title { font-weight: 700; margin-bottom: 10px; }
    .h1 { font-size: 1.8em; } .h2 { font-size: 1.4em; } .h3 { font-size: 1.2em; }
    .h4 { font-size: 1.05em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .layout { display: flex; gap: 12px; }
    .accordion { flex: 1; display: flex; flex-direction: column; gap: 0; }
    .acc-item { border-bottom: 1px solid var(--uui-color-border,#e3e3e3); }
    .acc-header {
      display: flex; justify-content: space-between; align-items: center;
      padding: 8px 0; cursor: pointer;
    }
    .acc-title { font-size: 13px; font-weight: 600; }
    .acc-arrow { font-size: 10px; color: var(--uui-color-text-alt,#767676); }
    .acc-body { font-size: 12px; line-height: 1.5; padding-bottom: 8px; color: var(--uui-color-text-alt,#767676); }
    .img-col { flex: 0 0 35%; }
    .img-box {
      display: flex; align-items: center; justify-content: center;
      background: var(--uui-color-surface-alt,#f5f5f5); border-radius: 4px;
      min-height: 100px; color: var(--uui-color-text-alt,#767676);
    }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin: 0; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 8px; }
  `];
}

customElements.define("dcms-features-with-descriptions-block-view", DcmsFeaturesWithDescriptionsBlockView);
