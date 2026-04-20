import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

// clientReview2Block — 3-column card grid style (desktop breakpoint shows 3 per view)
export default class DcmsClientReview2BlockView extends DcmsBlockBase {
  static properties = {
    ...DcmsBlockBase.properties,
    _page: { state: true },
  };

  constructor() {
    super();
    this._page = 0;
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
    const perPage = 3;
    const pages = Math.ceil(total / perPage) || 1;
    const slice = reviews.slice(this._page * perPage, this._page * perPage + perPage);

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="section-headline ${tag}">${headline}</div>` : ""}
        ${total === 0 ? html`<p class="empty">(no reviews)</p>` : html`
          <div class="grid">
            ${slice.map(item => html`
              <div class="card">
                <div class="stars">${this._stars(item?.rating)}</div>
                <div class="quote" .innerHTML=${(item?.quote || item?.richText?.markup || "(review text)").slice(0, 150)}></div>
                <div class="author">
                  <div class="avatar">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
                      <circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
                    </svg>
                  </div>
                  <div>
                    <div class="name">${item?.name || item?.reviewerName || "—"}</div>
                    ${item?.company || item?.position ? html`<div class="company">${item?.company || item?.position}</div>` : ""}
                  </div>
                </div>
              </div>
            `)}
          </div>
          ${pages > 1 ? html`
            <div class="controls">
              <button class="ctrl" @click=${() => this._page = Math.max(0, this._page - 1)}>‹</button>
              <span class="counter">${this._page + 1} / ${pages}</span>
              <button class="ctrl" @click=${() => this._page = Math.min(pages - 1, this._page + 1)}>›</button>
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
    .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
    .card {
      display: flex; flex-direction: column; gap: 5px;
      padding: 8px; border: 1px solid var(--uui-color-border,#e3e3e3);
      border-radius: 6px; background: var(--uui-color-surface,#fff);
    }
    .stars { color: #f5a623; font-size: 11px; letter-spacing: 1px; }
    .quote { font-size: 11px; line-height: 1.4; max-height: 50px; overflow: hidden; flex: 1; }
    .author { display: flex; align-items: center; gap: 6px; margin-top: 4px; }
    .avatar {
      width: 26px; height: 26px; border-radius: 50%; flex-shrink: 0;
      background: var(--uui-color-surface-alt,#f0f0f0);
      display: flex; align-items: center; justify-content: center;
    }
    .name { font-size: 10px; font-weight: 600; }
    .company { font-size: 10px; color: var(--uui-color-text-alt,#767676); }
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

customElements.define("dcms-client-review2-block-view", DcmsClientReview2BlockView);
