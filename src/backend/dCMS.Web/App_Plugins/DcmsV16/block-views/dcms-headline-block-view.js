import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsHeadlineBlockView extends DcmsBlockBase {
  render() {
    const c  = this.content || {};
    const tag = headlineTag(c.headlineLevel, "h2");
    const text = c.headlineText || c.headline || c.title || "(no headline)";
    const align = c.horizontalAlignment?.toLowerCase() || "left";

    return html`
      <div class="wrap" style="text-align:${align}">
        <div class="type-badge">${tag.toUpperCase()}</div>
        ${this.#renderTag(tag, text)}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  #renderTag(tag, text) {
    switch (tag) {
      case "h1": return html`<h1>${text}</h1>`;
      case "h2": return html`<h2>${text}</h2>`;
      case "h3": return html`<h3>${text}</h3>`;
      case "h4": return html`<h4>${text}</h4>`;
      case "h5": return html`<h5>${text}</h5>`;
      case "h6": return html`<h6>${text}</h6>`;
      default:   return html`<p class="h2">${text}</p>`;
    }
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; padding: 12px; }
    .wrap { }
    h1,h2,h3,h4,h5,h6,p { margin: 4px 0 8px; word-break: break-word; }
    h1 { font-size: 2em; } h2 { font-size: 1.6em; } h3 { font-size: 1.3em; }
    h4 { font-size: 1.1em; } h5 { font-size: 1em; } h6 { font-size: .9em; }
    .type-badge {
      display: inline-block; padding: 2px 7px; font-size: 10px; font-weight: 700;
      border-radius: 3px; background: var(--uui-color-current,#3544b1); color: #fff;
      margin-bottom: 6px; letter-spacing: .05em;
    }
    .css-badge {
      font-size: 11px; color: var(--uui-color-text-alt,#767676); font-style: italic;
      margin-top: 4px;
    }
  `];
}

customElements.define("dcms-headline-block-view", DcmsHeadlineBlockView);
