import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsRichTextBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const markup = c.richText?.markup || c.richText || "";
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";

    return html`
      <div class="wrap" style=${schemeStyle}>
        <div class="block-label">Rich Text</div>
        ${markup
          ? html`<div class="rich-text" .innerHTML=${markup}></div>`
          : html`<p class="empty">(no content)</p>`}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; min-height: 40px; }
    .block-label {
      font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: .05em;
      color: var(--uui-color-text-alt,#767676); margin-bottom: 8px;
    }
    .rich-text { font-size: 13px; line-height: 1.6; }
    .rich-text h1,.rich-text h2,.rich-text h3,.rich-text h4,.rich-text h5,.rich-text h6 {
      margin: 0.5em 0 0.3em;
    }
    .rich-text p { margin: 0.3em 0; }
    .rich-text img { max-width: 100%; height: auto; }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin: 0; }
  `];
}

customElements.define("dcms-rich-text-block-view", DcmsRichTextBlockView);
