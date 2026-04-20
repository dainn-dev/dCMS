import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsLayoutSectionsBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const bgColor = scheme?.content?.background || "transparent";
    const textColor = scheme?.content?.primary || "inherit";
    const bgKey = c.backgroundColor || "";

    return html`
      <div class="section-wrap" style="background:${bgColor};color:${textColor};">
        <div class="label-row">
          <span class="label">Layout Section</span>
          ${bgKey ? html`<span class="scheme-badge">Style: ${bgKey}</span>` : ""}
        </div>
        <slot></slot>
        <div class="inner-hint">Nested blocks rendered here</div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .section-wrap {
      padding: 10px 12px; border: 2px dashed var(--uui-color-border,#e3e3e3);
      border-radius: 4px; min-height: 48px;
    }
    .label-row { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
    .label { font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em; opacity: .6; }
    .scheme-badge {
      font-size: 10px; padding: 2px 6px; border-radius: 3px;
      background: var(--uui-color-current,#3544b1); color: #fff; font-weight: 600;
    }
    .inner-hint { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    slot { display: block; }
  `];
}

customElements.define("dcms-layout-sections-block-view", DcmsLayoutSectionsBlockView);
