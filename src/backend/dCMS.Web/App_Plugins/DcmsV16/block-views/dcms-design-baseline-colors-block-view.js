import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, hexToRgb } from "./dcms-block-base.js";

// designBaselineColors — block-level color override.
// Reads the `colors` property (an object with named color keys, e.g. background, primary, h1…h6, link)
// and renders a palette swatch grid so editors can see the override values at a glance.
export default class DcmsDesignBaselineColorsBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    // `colors` can be a flat object {background:'#fff', primary:'#333', ...}
    // or a nested object {content:{background,primary,...}, button:{fill,...}}
    const rawColors = c.colors || {};

    // Flatten nested structure into label→hex pairs
    const pairs = [];
    const add = (prefix, obj) => {
      if (typeof obj === "string") {
        pairs.push([prefix, obj]);
      } else if (obj && typeof obj === "object") {
        Object.entries(obj).forEach(([k, v]) => add(prefix ? `${prefix}.${k}` : k, v));
      }
    };
    add("", rawColors);

    return html`
      <div class="wrap">
        <div class="label-row">
          <span class="icon">🎨</span>
          <span class="title">Design Baseline Colors override</span>
        </div>
        ${pairs.length === 0 ? html`<p class="empty">(no colors set)</p>` : html`
          <div class="swatches">
            ${pairs.map(([label, hex]) => {
              const isValid = /^#[0-9a-fA-F]{3,8}$/.test(String(hex));
              return html`
                <div class="swatch-item" title="${label}: ${hex}">
                  <div class="swatch" style="${isValid ? `background:${hex};` : 'background:#eee;'}">
                    ${!isValid ? html`<span class="swatch-err">?</span>` : ""}
                  </div>
                  <span class="swatch-label">${label.split(".").pop()}</span>
                </div>
              `;
            })}
          </div>
        `}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 10px 12px; }
    .label-row { display: flex; align-items: center; gap: 6px; margin-bottom: 8px; }
    .icon { font-size: 14px; }
    .title { font-size: 12px; font-weight: 600; color: var(--uui-color-text,#333); }
    .swatches { display: flex; flex-wrap: wrap; gap: 6px; }
    .swatch-item { display: flex; flex-direction: column; align-items: center; gap: 3px; }
    .swatch {
      width: 32px; height: 32px; border-radius: 6px;
      border: 1px solid var(--uui-color-border,#d0d0d0);
      display: flex; align-items: center; justify-content: center;
    }
    .swatch-err { font-size: 10px; color: var(--uui-color-text-alt,#aaa); }
    .swatch-label { font-size: 9px; color: var(--uui-color-text-alt,#767676); max-width: 36px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: center; }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 6px; }
  `];
}

customElements.define("dcms-design-baseline-colors-block-view", DcmsDesignBaselineColorsBlockView);
