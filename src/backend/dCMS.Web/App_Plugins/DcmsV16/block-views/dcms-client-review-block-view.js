import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsClientReviewBlockView extends DcmsBlockBase {
  static properties = {
    ...DcmsBlockBase.properties,
    _idx: { state: true },
  };

  constructor() {
    super();
    this._idx = 0;
  }

  _stars(rating) {
    const n = Math.min(5, Math.max(0, parseInt(rating) || 5));
    return Array.from({ length: 5 }, (_, i) => i < n ? "★" : "☆").join("");
  }

  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h2");
    const headline = c.headline || c.headlineText || "";
    const subheadline = c.subheadline || c.subtitle || "";
    const reviews = c.clientReviews?.contentData || [];
    const total = reviews.length;
    const item = reviews[this._idx] || null;

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="section-headline ${tag}">${headline}</div>` : ""}
        ${subheadline ? html`<div class="sub">${subheadline}</div>` : ""}
        ${total === 0 ? html`<p class="empty">(no reviews)</p>` : html`
          <div class="slide">
            <div class="avatar">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
                <circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
              </svg>
            </div>
            <div class="content">
              <div class="stars">${this._stars(item?.rating)}</div>
              ${item?.quote || item?.richText?.markup
                ? html`<div class="quote" .innerHTML=${(item?.quote || item?.richText?.markup || "").slice(0, 180)}></div>`
                : html`<div class="quote empty">(review text)</div>`}
              <div class="author">
                <span class="name">${item?.name || item?.reviewerName || "—"}</span>
                ${item?.company || item?.position ? html`<span class="company"> · ${item?.company || item?.position}</span>` : ""}
              </div>
            </div>
          </div>
          ${total > 1 ? html`
            <div class="controls">
              <button class="ctrl" @click=${() => this._idx = (this._idx - 1 + total) % total}>‹</button>
              <span class="counter">${this._idx + 1} / ${total}</span>
              <button class="ctrl" @click=${() => this._idx = (this._idx + 1) % total}>›</button>
            </div>
          ` : ""}
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
    .slide {
      display: flex; gap: 10px; align-items: flex-start;
      padding: 10px; background: var(--uui-color-surface-alt,#f9f9f9);
      border-radius: 6px; border: 1px solid var(--uui-color-border,#e3e3e3);
    }
    .avatar {
      flex-shrink: 0; width: 40px; height: 40px; border-radius: 50%;
      background: var(--uui-color-border,#e3e3e3);
      display: flex; align-items: center; justify-content: center;
      color: var(--uui-color-text-alt,#767676);
    }
    .content { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 0; }
    .stars { color: #f5a623; font-size: 13px; letter-spacing: 1px; }
    .quote { font-size: 12px; line-height: 1.5; max-height: 60px; overflow: hidden; }
    .quote.empty { color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .author { font-size: 11px; font-weight: 600; }
    .company { font-weight: 400; color: var(--uui-color-text-alt,#767676); }
    .controls { display: flex; align-items: center; justify-content: center; gap: 10px; margin-top: 8px; }
    .ctrl {
      width: 24px; height: 24px; border-radius: 50%; border: 1px solid var(--uui-color-border,#e3e3e3);
      background: #fff; cursor: pointer; font-size: 14px; display: flex; align-items: center; justify-content: center;
    }
    .ctrl:hover { background: var(--uui-color-surface-alt,#f5f5f5); }
    .counter { font-size: 11px; color: var(--uui-color-text-alt,#767676); }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 6px; }
  `];
}

customElements.define("dcms-client-review-block-view", DcmsClientReviewBlockView);
