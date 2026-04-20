import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsTeamMembersBlockView extends DcmsBlockBase {
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
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h2");
    const headline = c.headline || c.headlineText || c.title || "";
    const subheadline = c.subheadline || c.subtitle || "";
    const items = c.items?.contentData || [];
    const total = items.length;
    // Show a sliding preview of 3 members at a time
    const visible = items.slice(this._idx, this._idx + 3);

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="headline ${tag}">${headline}</div>` : ""}
        ${subheadline ? html`<div class="sub">${subheadline}</div>` : ""}
        ${total === 0 ? html`<p class="empty">(no team members)</p>` : html`
          <div class="grid">
            ${visible.map(item => html`
              <div class="member">
                <div class="avatar">
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
                    <circle cx="12" cy="8" r="4"/>
                    <path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
                  </svg>
                </div>
                <div class="name">${item.name || item.headline || item.title || "—"}</div>
                ${item.role || item.position ? html`<div class="role">${item.role || item.position}</div>` : ""}
              </div>
            `)}
          </div>
          ${total > 3 ? html`
            <div class="controls">
              <button class="ctrl" @click=${() => this._idx = Math.max(0, this._idx - 3)}>‹</button>
              <span class="counter">${Math.floor(this._idx / 3) + 1} / ${Math.ceil(total / 3)}</span>
              <button class="ctrl" @click=${() => this._idx = Math.min(total - 3, this._idx + 3)}>›</button>
            </div>
          ` : html`<div class="count">${total} member${total !== 1 ? "s" : ""}</div>`}
        `}
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; }
    .headline { font-weight: 700; margin-bottom: 4px; }
    .h1 { font-size: 1.8em; } .h2 { font-size: 1.4em; } .h3 { font-size: 1.2em; }
    .h4 { font-size: 1.05em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .sub { font-size: 12px; color: var(--uui-color-text-alt,#767676); margin-bottom: 10px; }
    .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
    .member {
      display: flex; flex-direction: column; align-items: center; gap: 4px;
      padding: 8px; border: 1px solid var(--uui-color-border,#e3e3e3);
      border-radius: 4px;
    }
    .avatar {
      width: 40px; height: 40px; border-radius: 50%;
      background: var(--uui-color-surface-alt,#f5f5f5);
      display: flex; align-items: center; justify-content: center;
      color: var(--uui-color-text-alt,#767676);
    }
    .name { font-size: 11px; font-weight: 600; text-align: center; }
    .role { font-size: 10px; color: var(--uui-color-text-alt,#767676); text-align: center; }
    .controls { display: flex; align-items: center; justify-content: center; gap: 10px; margin-top: 8px; }
    .ctrl {
      width: 24px; height: 24px; border-radius: 50%; border: 1px solid var(--uui-color-border,#e3e3e3);
      background: #fff; cursor: pointer; font-size: 14px; display: flex; align-items: center; justify-content: center;
    }
    .ctrl:hover { background: var(--uui-color-surface-alt,#f5f5f5); }
    .counter { font-size: 11px; color: var(--uui-color-text-alt,#767676); }
    .count { font-size: 11px; color: var(--uui-color-text-alt,#767676); margin-top: 6px; text-align: center; }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 6px; }
  `];
}

customElements.define("dcms-team-members-block-view", DcmsTeamMembersBlockView);
