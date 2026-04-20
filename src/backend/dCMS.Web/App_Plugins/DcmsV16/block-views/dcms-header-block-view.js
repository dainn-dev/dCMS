import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsHeaderBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const links   = c.headerLinks?.contentData || [];
    const buttons = c.headerButtons?.contentData || [];
    const logo    = c.logo?.length ? c.logo[0] : null;
    const siteName = c.siteName || c.websiteName || "";

    return html`
      <nav class="navbar">
        <div class="brand">
          ${logo
            ? html`<div class="logo-placeholder"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg></div>`
            : ""}
          ${siteName ? html`<span class="site-name">${siteName}</span>` : html`<span class="site-name">Site Name</span>`}
        </div>
        <div class="nav-links">
          ${links.map(l => html`<span class="nav-link">${l.linkTitle || l.name || "Link"}</span>`)}
          ${buttons.map(b => html`<span class="nav-btn">${b.linkTitle || b.name || "Button"}</span>`)}
          ${!links.length && !buttons.length ? html`<span class="nav-link empty">Nav Links</span>` : ""}
        </div>
      </nav>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .navbar {
      display: flex; align-items: center; justify-content: space-between;
      padding: 10px 16px; background: var(--uui-color-surface,#fff);
      border-bottom: 2px solid var(--uui-color-border,#e3e3e3); min-height: 48px;
    }
    .brand { display: flex; align-items: center; gap: 8px; }
    .logo-placeholder {
      width: 32px; height: 32px; background: var(--uui-color-surface-alt,#f5f5f5);
      border-radius: 4px; display: flex; align-items: center; justify-content: center;
      color: var(--uui-color-text-alt,#767676);
    }
    .site-name { font-weight: 700; font-size: 14px; }
    .nav-links { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .nav-link { font-size: 12px; color: var(--uui-color-text,#333); padding: 2px 6px; }
    .nav-link.empty { color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .nav-btn {
      font-size: 12px; font-weight: 600; padding: 3px 10px; border-radius: 4px;
      background: var(--uui-color-current,#3544b1); color: #fff;
    }
  `];
}

customElements.define("dcms-header-block-view", DcmsHeaderBlockView);
