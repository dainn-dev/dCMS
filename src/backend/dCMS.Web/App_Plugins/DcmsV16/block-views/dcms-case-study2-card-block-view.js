import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsCaseStudy2CardBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tagScheme = this._resolveScheme(c.tagBackgroundColor);
    const tagStyle = tagScheme ? this._schemeCssVars(tagScheme) : "";
    const tag = headlineTag(c.headlineLevel, "h4");
    const headline = c.headline || c.headlineText || c.title || "";
    const category = c.category || c.tag || c.label || null;
    const excerpt = c.excerpt || c.richText?.markup || c.subtitle || "";
    const img = Array.isArray(c.image) ? c.image[0] : null;
    const linkName = Array.isArray(c.link) ? c.link[0]?.name : null;
    // cornerRadius for picture (design.cornerRadius.picturesAndVideo)
    const picRadius = c.pictureCornerRadius ?? c.cornerRadius ?? null;
    const imgStyle = picRadius !== null ? `border-radius:${picRadius}px;` : "";

    return html`
      <div class="card" style="${schemeStyle}">
        <div class="card-media" style="${imgStyle}">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <circle cx="8.5" cy="8.5" r="1.5"/>
            <polyline points="21 15 16 10 5 21"/>
          </svg>
          ${img?.name ? html`<span class="img-name">${img.name}</span>` : ""}
        </div>
        <div class="card-body">
          ${category ? html`
            <div class="badge" style="${tagStyle}">${category}</div>
          ` : ""}
          ${headline ? html`<div class="headline ${tag}">${headline}</div>` : ""}
          ${excerpt ? html`<div class="excerpt" .innerHTML=${excerpt.slice(0, 160)}></div>` : ""}
          ${linkName ? html`
            <div class="btn-preview">${linkName} →</div>
          ` : ""}
        </div>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .card {
      border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 6px;
      overflow: hidden; display: flex; flex-direction: column;
      box-shadow: 0 2px 8px rgba(0,0,0,0.06);
    }
    .card-media {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      height: 90px; background: var(--uui-color-surface-alt,#f5f5f5);
      color: var(--uui-color-text-alt,#767676); gap: 4px; overflow: hidden;
    }
    .img-name { font-size: 10px; }
    .card-body { padding: 10px 12px; display: flex; flex-direction: column; gap: 6px; }
    .badge {
      display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 10px; font-weight: 700;
      background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff);
      align-self: flex-start;
    }
    .headline { font-weight: 700; }
    .h1{font-size:1.8em} .h2{font-size:1.4em} .h3{font-size:1.2em}
    .h4{font-size:1.05em} .h5{font-size:1em} .h6{font-size:.9em}
    .excerpt { font-size: 12px; line-height: 1.5; max-height: 50px; overflow: hidden; }
    .btn-preview { font-size: 11px; font-weight: 600; color: var(--block-btn-bg,#2986cc); cursor: default; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; padding: 0 12px 6px; }
  `];
}

customElements.define("dcms-case-study2-card-block-view", DcmsCaseStudy2CardBlockView);
