import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsBanner3BlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const bgColor = scheme?.content?.background || "#f8f8f8";
    const textColor = scheme?.content?.primary || "#1a1a1a";
    const tag = headlineTag(c.headlineLevel, "h1");
    const title = c.title || c.headline || c.headlineText || "(title)";
    const text = c.text || "";
    const circleText = c.circleText || "";

    return html`
      <div class="banner3" style="background:${bgColor};color:${textColor}">
        <div class="left-img-placeholder">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
        </div>
        <div class="center">
          <div class="badge">${tag.toUpperCase()} · Banner 3</div>
          <div class="title ${tag}">${title}</div>
          ${text ? html`<p class="text">${text}</p>` : ""}
          ${circleText ? html`<div class="circle-preview">${circleText}</div>` : ""}
        </div>
        <div class="right-img-placeholder">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".5"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .banner3 {
      display: flex; align-items: center; gap: 12px; padding: 16px 12px;
      border-radius: 4px; min-height: 80px; position: relative; overflow: hidden;
    }
    .left-img-placeholder, .right-img-placeholder {
      flex: 0 0 50px; min-height: 60px; background: rgba(0,0,0,.05);
      border-radius: 4px; display: flex; align-items: center; justify-content: center;
    }
    .center { flex: 1; display: flex; flex-direction: column; align-items: center; gap: 4px; text-align: center; }
    .badge { font-size: 10px; font-weight: 700; opacity: .6; text-transform: uppercase; letter-spacing: .05em; }
    .title { font-weight: 700; margin: 0; }
    .h1 { font-size: 1.8em; } .h2 { font-size: 1.4em; } .h3 { font-size: 1.2em; }
    .h4 { font-size: 1.05em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .text { font-size: 12px; margin: 0; opacity: .85; }
    .circle-preview {
      display: inline-block; padding: 4px 8px; border-radius: 50px;
      background: rgba(0,0,0,.1); font-size: 10px; font-style: italic;
    }
  `];
}

customElements.define("dcms-banner3-block-view", DcmsBanner3BlockView);
