import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase } from "./dcms-block-base.js";

export default class DcmsFooterBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const links   = c.footerLinks?.contentData || [];
    const social  = c.socialMediaButtons?.contentData || [];
    const siteName = c.siteName || c.websiteName || "";
    const copyright = c.copyrightMessage || c.copyright || "";

    return html`
      <footer class="footer">
        <div class="footer-top">
          <div class="brand">
            <span class="site-name">${siteName || "Site Name"}</span>
          </div>
          <div class="footer-links">
            ${links.map(l => html`<span class="f-link">${l.linkTitle || l.name || "Link"}</span>`)}
            ${!links.length ? html`<span class="f-link empty">Footer Links</span>` : ""}
          </div>
          ${social.length ? html`
            <div class="social-row">
              ${social.map(s => html`<span class="social-btn">${s.platform || s.name || "Social"}</span>`)}
            </div>
          ` : ""}
        </div>
        ${copyright ? html`<div class="copyright">${copyright}</div>` : ""}
      </footer>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .footer {
      background: var(--uui-color-surface-alt,#f5f5f5);
      border-top: 2px solid var(--uui-color-border,#e3e3e3);
      padding: 12px 16px;
    }
    .footer-top { display: flex; align-items: flex-start; gap: 16px; flex-wrap: wrap; }
    .brand { flex: 0 0 auto; }
    .site-name { font-weight: 700; font-size: 13px; }
    .footer-links { display: flex; flex-wrap: wrap; gap: 8px; flex: 1; align-items: center; }
    .f-link { font-size: 11px; color: var(--uui-color-text,#333); }
    .f-link.empty { color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .social-row { display: flex; gap: 6px; flex-wrap: wrap; }
    .social-btn {
      font-size: 10px; padding: 2px 6px; border-radius: 3px;
      background: var(--uui-color-border,#e3e3e3); color: var(--uui-color-text,#333);
    }
    .copyright { font-size: 10px; color: var(--uui-color-text-alt,#767676); margin-top: 8px; }
  `];
}

customElements.define("dcms-footer-block-view", DcmsFooterBlockView);
