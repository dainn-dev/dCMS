import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsCarouselBlockView extends DcmsBlockBase {
  static properties = {
    ...DcmsBlockBase.properties,
    _idx: { state: true },
  };

  constructor() {
    super();
    this._idx = 0;
  }

  render() {
    const c = this.content || {};
    const images = Array.isArray(c.images) ? c.images : [];
    const total = images.length;
    const current = images[this._idx] || null;
    const imgName = current?.name || current?.mediaKey || null;

    return html`
      <div class="carousel">
        <div class="slide">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <circle cx="8.5" cy="8.5" r="1.5"/>
            <polyline points="21 15 16 10 5 21"/>
          </svg>
          ${imgName ? html`<span class="img-name">${imgName}</span>` : html`<span class="img-name">No images</span>`}
        </div>
        ${total > 1 ? html`
          <div class="controls">
            <button class="ctrl-btn" @click=${() => this._idx = (this._idx - 1 + total) % total}>‹</button>
            <span class="counter">${this._idx + 1} / ${total}</span>
            <button class="ctrl-btn" @click=${() => this._idx = (this._idx + 1) % total}>›</button>
          </div>
        ` : total === 1 ? html`<div class="counter single">1 image</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .carousel { padding: 10px 12px; }
    .slide {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      min-height: 80px; background: var(--uui-color-surface-alt,#f5f5f5);
      border-radius: 4px; gap: 6px; color: var(--uui-color-text-alt,#767676);
    }
    .img-name { font-size: 11px; }
    .controls { display: flex; align-items: center; justify-content: center; gap: 12px; margin-top: 8px; }
    .ctrl-btn {
      width: 28px; height: 28px; border-radius: 50%; border: 1px solid var(--uui-color-border,#e3e3e3);
      background: #fff; cursor: pointer; font-size: 16px; display: flex; align-items: center; justify-content: center;
    }
    .ctrl-btn:hover { background: var(--uui-color-surface-alt,#f5f5f5); }
    .counter { font-size: 11px; color: var(--uui-color-text-alt,#767676); }
    .counter.single { margin-top: 6px; text-align: center; }
  `];
}

customElements.define("dcms-carousel-block-view", DcmsCarouselBlockView);
