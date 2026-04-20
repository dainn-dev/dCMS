import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsBreadcrumbBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    // customItems can be a block list of manual crumbs; otherwise show static placeholder
    const customItems = c.items?.contentData || c.breadcrumbItems?.contentData || [];

    return html`
      <div class="wrap" style="${schemeStyle}">
        <nav class="breadcrumb" aria-label="breadcrumb">
          ${customItems.length > 0
            ? customItems.map((item, i) => html`
                <span class="crumb ${i === customItems.length - 1 ? 'active' : ''}">
                  ${item.label || item.title || item.name || `Page ${i + 1}`}
                </span>
                ${i < customItems.length - 1 ? html`<span class="sep">›</span>` : ""}
              `)
            : html`
                <span class="crumb"><span class="home-icon">⌂</span> Home</span>
                <span class="sep">›</span>
                <span class="crumb">Section</span>
                <span class="sep">›</span>
                <span class="crumb active">Current Page</span>
              `}
        </nav>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 8px 12px; }
    .breadcrumb { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; list-style: none; margin: 0; padding: 0; }
    .crumb { font-size: 12px; color: var(--block-link,#2986cc); cursor: default; }
    .crumb.active { color: var(--uui-color-text-alt,#767676); font-weight: 600; }
    .sep { font-size: 12px; color: var(--uui-color-text-alt,#aaa); }
    .home-icon { font-size: 13px; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 4px; }
  `];
}

customElements.define("dcms-breadcrumb-block-view", DcmsBreadcrumbBlockView);
