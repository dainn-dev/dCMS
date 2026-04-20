import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsCardBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h5");
    const headline = c.headline || c.headlineText || c.title || "";
    const markup = c.richText?.markup || "";
    const linkName = Array.isArray(c.link) ? c.link[0]?.name : null;
    const variant = c.variant || "Primary";
    const img = Array.isArray(c.image) ? c.image[0] : null;

    return html`
      <div class="card" style=${schemeStyle}>
        <div class="card-media">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <circle cx="8.5" cy="8.5" r="1.5"/>
            <polyline points="21 15 16 10 5 21"/>
          </svg>
          ${img?.name ? html`<span class="img-name">${img.name}</span>` : ""}
        </div>
        <div class="card-body">
          ${headline ? html`<div class="headline ${tag}">${headline}</div>` : ""}
          <hr class="divider"/>
          ${markup ? html`<div class="text" .innerHTML=${markup.slice(0,200)}></div>` : ""}
          ${linkName ? html`
            <div class="btn-preview ${variant === "Secondary" ? "sec" : "pri"}">${linkName}</div>
          ` : ""}
        </div>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .card { border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 6px; overflow: hidden; display: flex; flex-direction: column; }
    .card-media {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      height: 80px; background: var(--uui-color-surface-alt,#f5f5f5);
      color: var(--uui-color-text-alt,#767676); gap: 4px;
    }
    .img-name { font-size: 10px; }
    .card-body { padding: 10px 12px; display: flex; flex-direction: column; gap: 6px; }
    .headline { font-weight: 700; }
    .h1 { font-size: 1.8em; } .h2 { font-size: 1.4em; } .h3 { font-size: 1.2em; }
    .h4 { font-size: 1.05em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .divider { border: none; border-top: 1px solid var(--uui-color-border,#e3e3e3); margin: 2px 0; }
    .text { font-size: 12px; line-height: 1.5; max-height: 60px; overflow: hidden; }
    .btn-preview {
      display: inline-block; padding: 4px 10px; border-radius: 4px; font-size: 11px;
      font-weight: 600; align-self: flex-start;
    }
    .btn-preview.pri { background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff); }
    .btn-preview.sec { background: transparent; border: 1px solid var(--block-btn-border,#2986cc); color: var(--block-btn-border,#2986cc); }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; padding: 4px 12px 6px; }
  `];
}

customElements.define("dcms-card-block-view", DcmsCardBlockView);
