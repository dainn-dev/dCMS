import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsTeamMemberBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const name = c.name || c.headline || c.title || "";
    const role = c.role || c.position || c.subtitle || "";
    const bio = c.richText?.markup || c.bio || "";
    const img = Array.isArray(c.image) ? c.image[0] : null;
    const imgName = img?.name || null;
    const cornerRadius = c.cornerRadius ?? null;
    const shadow = "box-shadow: 0 2px 8px rgba(0,0,0,0.08);";
    const cardStyle = `${cornerRadius !== null ? `border-radius:${cornerRadius}px;` : "border-radius:6px;"}${shadow}`;

    return html`
      <div class="member" style="${schemeStyle}${cardStyle}">
        <div class="avatar">
          <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5">
            <circle cx="12" cy="8" r="4"/>
            <path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>
          </svg>
          ${imgName ? html`<span class="img-name">${imgName}</span>` : ""}
        </div>
        <div class="info">
          ${name ? html`<div class="name">${name}</div>` : html`<div class="name empty">(no name)</div>`}
          ${role ? html`<div class="role">${role}</div>` : ""}
          ${bio ? html`<div class="bio" .innerHTML=${bio.slice(0, 120)}></div>` : ""}
        </div>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .member {
      border: 1px solid var(--uui-color-border,#e3e3e3); overflow: hidden;
      display: flex; flex-direction: column; align-items: center;
      padding: 12px; gap: 8px;
    }
    .avatar {
      width: 64px; height: 64px; border-radius: 50%;
      background: var(--uui-color-surface-alt,#f5f5f5);
      display: flex; flex-direction: column; align-items: center; justify-content: center;
      color: var(--uui-color-text-alt,#767676); gap: 2px; overflow: hidden;
    }
    .img-name { font-size: 8px; text-align: center; padding: 0 4px; }
    .info { text-align: center; display: flex; flex-direction: column; gap: 4px; width: 100%; }
    .name { font-size: 13px; font-weight: 700; }
    .name.empty { color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .role { font-size: 11px; color: var(--uui-color-text-alt,#767676); }
    .bio { font-size: 11px; line-height: 1.5; max-height: 40px; overflow: hidden; color: var(--uui-color-text,#333); }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
  `];
}

customElements.define("dcms-team-member-block-view", DcmsTeamMemberBlockView);
