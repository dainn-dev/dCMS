import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsEmbedCodeBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const code = c.code || "";
    const preview = code.slice(0, 120).replace(/</g, "&lt;").replace(/>/g, "&gt;");

    return html`
      <div class="wrap">
        <div class="icon-row">
          <svg width="28" height="28" viewBox="0 0 16 16" fill="currentColor">
            <path d="M10.478 1.647a.5.5 0 1 0-.956-.294l-4 13a.5.5 0 0 0 .956.294zM4.854 4.146a.5.5 0 0 1 0 .708L1.707 8l3.147 3.146a.5.5 0 0 1-.708.708l-3.5-3.5a.5.5 0 0 1 0-.708l3.5-3.5a.5.5 0 0 1 .708 0m6.292 0a.5.5 0 0 0 0 .708L14.293 8l-3.147 3.146a.5.5 0 0 0 .708.708l3.5-3.5a.5.5 0 0 0 0-.708l-3.5-3.5a.5.5 0 0 0-.708 0"/>
          </svg>
          <span class="label">Embed Code</span>
        </div>
        ${code
          ? html`<pre class="code-preview">${preview}${code.length > 120 ? "…" : ""}</pre>`
          : html`<p class="empty">(no embed code)</p>`}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; display: flex; flex-direction: column; align-items: center; gap: 8px; min-height: 60px; }
    .icon-row { display: flex; align-items: center; gap: 8px; color: var(--uui-color-text-alt,#767676); }
    .label { font-size: 12px; font-weight: 600; }
    .code-preview {
      font-size: 10px; font-family: monospace; background: var(--uui-color-surface-alt,#f5f5f5);
      border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 4px; padding: 6px 8px;
      white-space: pre-wrap; word-break: break-all; max-height: 80px; overflow: hidden;
      width: 100%; box-sizing: border-box; color: var(--uui-color-text,#333);
    }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin: 0; }
  `];
}

customElements.define("dcms-embed-code-block-view", DcmsEmbedCodeBlockView);
