import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsClientsAndPartnersBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h2");
    const headline = c.headline || c.headlineText || "";
    const subheadline = c.subheadline || c.subtitle || "";
    // logos can be a nested block list or simple media array
    const logos = c.logos?.contentData || c.partners?.contentData
      || (Array.isArray(c.logos) ? c.logos : [])
      || [];

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="section-headline ${tag}">${headline}</div>` : ""}
        ${subheadline ? html`<div class="sub">${subheadline}</div>` : ""}
        ${logos.length === 0 ? html`<p class="empty">(no logos)</p>` : html`
          <div class="grid">
            ${logos.slice(0, 8).map(logo => {
              const name = logo.name || logo.title
                || (Array.isArray(logo.logo) ? logo.logo[0]?.name : null)
                || (Array.isArray(logo.image) ? logo.image[0]?.name : null)
                || "Logo";
              return html`
                <div class="logo-cell" title="${name}">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".4">
                    <rect x="3" y="3" width="18" height="18" rx="2"/>
                    <circle cx="8.5" cy="8.5" r="1.5"/>
                    <polyline points="21 15 16 10 5 21"/>
                  </svg>
                  <span class="label">${name}</span>
                </div>
              `;
            })}
            ${logos.length > 8 ? html`<div class="more-cell">+${logos.length - 8}</div>` : ""}
          </div>
        `}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; }
    .section-headline { font-weight: 700; margin-bottom: 4px; }
    .h1{font-size:1.8em} .h2{font-size:1.4em} .h3{font-size:1.2em}
    .h4{font-size:1.05em} .h5{font-size:1em} .h6{font-size:.9em}
    .sub { font-size: 12px; color: var(--uui-color-text-alt,#767676); margin-bottom: 10px; }
    .grid {
      display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px;
    }
    .logo-cell {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      gap: 4px; padding: 8px 4px; height: 56px;
      border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 4px;
      background: var(--uui-color-surface-alt,#f5f5f5);
      color: var(--uui-color-text-alt,#767676);
    }
    .label { font-size: 9px; text-align: center; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%; }
    .more-cell {
      display: flex; align-items: center; justify-content: center;
      height: 56px; border: 1px dashed var(--uui-color-border,#e3e3e3);
      border-radius: 4px; font-size: 12px; color: var(--uui-color-text-alt,#767676);
    }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 6px; }
  `];
}

customElements.define("dcms-clients-and-partners-block-view", DcmsClientsAndPartnersBlockView);
