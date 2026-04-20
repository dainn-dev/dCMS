import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

// clientReview3Block — large featured quote style (single slide, full width)
export default class DcmsClientReview3BlockView extends DcmsBlockBase {
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
    const reviews = c.clientReviews?.contentData || [];
    const total = reviews.length;
    const item = reviews[this._idx] || null;
    const img = Array.isArray(item?.image) ? item.image[0] : null;

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="section-headline ${tag}">${headline}</div>` : ""}
        ${total === 0 ? html`<p class="empty">(no reviews)</p>` : html`
          <div class="featured-slide">
            <div class="quote-mark">"</div>
            <div class="quote" .innerHTML=${(item?.quote || item?.richText?.markup || "(review text)").slice(0, 300)}></div>
            <div class="stars">${this._stars(item?.rating)}</div>
            <div class="author-row">
              <div class="avatar ${img ? 'has-img' : ''}">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
                  <circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
                </svg>
                ${img?.name ? html`<span class="img-hint">${img.name}</span>` : ""}
              </div>
              <div>
                <div class="name">${item?.name || item?.reviewerName || "—"}</div>
                ${item?.company || item?.position ? html`<div class="company">${item?.company || item?.position}</div>` : ""}
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
    .section-headline { font-weight: 700; margin-bottom: 10px; }
    .h1{font-size:1.8em} .h2{font-size:1.4em} .h3{font-size:1.2em}
    .h4{font-size:1.05em} .h5{font-size:1em} .h6{font-size:.9em}
    .featured-slide {
      display: flex; flex-direction: column; gap: 8px;
      padding: 14px; text-align: center;
      border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 8px;
      background: var(--uui-color-surface-alt,#f9f9f9);
    }
    .quote-mark { font-size: 40px; line-height: 1; color: var(--block-btn-bg,#2986cc); font-family: serif; margin-bottom: -8px; }
    .quote { font-size: 13px; line-height: 1.6; font-style: italic; max-height: 80px; overflow: hidden; }
    .stars { color: #f5a623; font-size: 14px; letter-spacing: 2px; }
    .author-row { display: flex; align-items: center; justify-content: center; gap: 8px; }
    .avatar {
      width: 36px; height: 36px; border-radius: 50%; flex-shrink: 0;
      background: var(--uui-color-border,#e3e3e3);
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      color: var(--uui-color-text-alt,#767676); overflow: hidden;
    }
    .img-hint { font-size: 7px; text-align: center; padding: 0 2px; }
    .name { font-size: 12px; font-weight: 700; }
    .company { font-size: 11px; color: var(--uui-color-text-alt,#767676); }
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

customElements.define("dcms-client-review3-block-view", DcmsClientReview3BlockView);
