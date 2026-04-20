import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsImageBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const img = Array.isArray(c.image) ? c.image[0] : null;
    const imgName = img?.name || img?.mediaKey || null;

    return html`
      <div class="wrap">
        <div class="img-box">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <circle cx="8.5" cy="8.5" r="1.5"/>
            <polyline points="21 15 16 10 5 21"/>
          </svg>
          ${imgName ? html`<span class="img-name">${imgName}</span>` : html`<span class="img-name">No image selected</span>`}
        </div>
        <div class="meta-row">
          <span class="meta-item">Image Block</span>
          ${c.alignment ? html`<span class="meta-item">${c.alignment}</span>` : ""}
          ${c.customCssClasses ? html`<span class="meta-item css">${c.customCssClasses}</span>` : ""}
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; }
    .img-box {
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      min-height: 80px; border: 2px dashed var(--uui-color-border,#e3e3e3);
      border-radius: 6px; gap: 6px; color: var(--uui-color-text-alt,#767676);
      padding: 12px;
    }
    .img-name { font-size: 12px; text-align: center; word-break: break-all; }
    .meta-row { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 8px; }
    .meta-item {
      font-size: 10px; padding: 2px 6px; border-radius: 3px;
      background: var(--uui-color-surface-alt,#f5f5f5);
      color: var(--uui-color-text-alt,#767676); font-weight: 600;
    }
    .meta-item.css { font-style: italic; font-weight: 400; }
  `];
}

customElements.define("dcms-image-block-view", DcmsImageBlockView);
