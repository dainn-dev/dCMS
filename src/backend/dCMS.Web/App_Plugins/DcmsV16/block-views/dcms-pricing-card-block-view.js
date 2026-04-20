import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsPricingCardBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h4");
    const tierName = c.tierName || c.headline || c.title || "";
    const price = c.price || c.priceText || null;
    const currency = c.currency || "$";
    const period = c.billingPeriod || c.period || "/ mo";
    const features = c.prosAndCons?.contentData || c.features?.contentData || [];
    const linkName = Array.isArray(c.link) ? c.link[0]?.name : null;
    const variant = c.variant || "Primary";
    const isPopular = c.isPopular == 1 || c.featured == 1;
    // CornerRadius from CornerRadius property
    const cornerRadius = c.cornerRadius ?? null;
    const cardStyle = `${cornerRadius !== null ? `border-radius:${cornerRadius}px;` : "border-radius:6px;"}box-shadow:0 2px 12px rgba(0,0,0,0.08);`;

    return html`
      <div class="card" style="${schemeStyle}${cardStyle}">
        ${isPopular ? html`<div class="badge">Popular</div>` : ""}
        <div class="card-head">
          ${tierName ? html`<div class="tier ${tag}">${tierName}</div>` : html`<div class="tier empty">(tier name)</div>`}
          ${price !== null ? html`
            <div class="price">
              <span class="currency">${currency}</span>
              <span class="amount">${price}</span>
              <span class="period">${period}</span>
            </div>
          ` : ""}
        </div>
        <hr class="divider"/>
        ${features.length ? html`
          <ul class="features">
            ${features.slice(0, 6).map(f => html`
              <li class="${f.disabled == 1 ? 'disabled' : ''}">
                <span class="icon">${f.disabled == 1 ? "✕" : "✓"}</span>
                ${f.text || f.headline || f.feature || "—"}
              </li>
            `)}
            ${features.length > 6 ? html`<li class="more">+${features.length - 6} more</li>` : ""}
          </ul>
        ` : ""}
        ${linkName ? html`
          <div class="btn-wrap">
            <div class="btn-preview ${variant === "Secondary" ? "sec" : "pri"}">${linkName}</div>
          </div>
        ` : ""}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .card {
      border: 1px solid var(--uui-color-border,#e3e3e3); overflow: hidden;
      display: flex; flex-direction: column; position: relative;
    }
    .badge {
      position: absolute; top: 8px; right: 8px;
      background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff);
      font-size: 10px; font-weight: 700; padding: 2px 7px; border-radius: 10px;
    }
    .card-head { padding: 12px 12px 8px; }
    .tier { font-weight: 700; margin-bottom: 6px; }
    .tier.empty { color: var(--uui-color-text-alt,#767676); font-style: italic; font-size: 13px; }
    .h1 { font-size: 1.8em; } .h2 { font-size: 1.4em; } .h3 { font-size: 1.2em; }
    .h4 { font-size: 1.05em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .price { display: flex; align-items: baseline; gap: 2px; }
    .currency { font-size: 14px; font-weight: 600; align-self: flex-start; margin-top: 4px; }
    .amount { font-size: 28px; font-weight: 800; line-height: 1; }
    .period { font-size: 11px; color: var(--uui-color-text-alt,#767676); }
    .divider { border: none; border-top: 1px solid var(--uui-color-border,#e3e3e3); margin: 0 12px; }
    .features { margin: 8px 0 6px; padding: 0 12px; list-style: none; display: flex; flex-direction: column; gap: 4px; }
    .features li { font-size: 11px; display: flex; align-items: center; gap: 5px; }
    .features li.disabled { color: var(--uui-color-text-alt,#767676); text-decoration: line-through; }
    .icon { font-size: 10px; font-weight: 700; color: var(--block-btn-bg,#2986cc); min-width: 12px; }
    .features li.disabled .icon { color: var(--uui-color-text-alt,#999); }
    .more { font-style: italic; color: var(--uui-color-text-alt,#767676); }
    .btn-wrap { padding: 6px 12px 10px; }
    .btn-preview {
      display: inline-block; padding: 5px 14px; border-radius: 4px; font-size: 12px; font-weight: 600; width: 100%;
      text-align: center; box-sizing: border-box;
    }
    .btn-preview.pri { background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff); }
    .btn-preview.sec { background: transparent; border: 1px solid var(--block-btn-border,#2986cc); color: var(--block-btn-border,#2986cc); }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; padding: 0 12px 6px; }
  `];
}

customElements.define("dcms-pricing-card-block-view", DcmsPricingCardBlockView);
