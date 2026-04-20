import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsHeroBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const bgColor = scheme?.content?.background || "#1a1a2e";
    const textColor = scheme?.content?.primary || "#fff";
    const tag = headlineTag(c.headlineLevel, "h1");
    const headline = c.headline || c.headlineText || c.title || "(hero headline)";
    const subHeadline = c.subHeadline || c.subTitle || c.text || "";
    const hasImage = Array.isArray(c.image) && c.image.length > 0;
    const vAlign = c.contentVerticalPosition?.toLowerCase() || "center";
    const hAlign = c.contentHorizontalPosition?.toLowerCase() || "center";
    const hAlignCss = hAlign === "left" ? "flex-start" : hAlign === "right" ? "flex-end" : "center";

    return html`
      <div class="hero" style="background:${bgColor};color:${textColor};align-items:${vAlign === "top" ? "flex-start" : vAlign === "bottom" ? "flex-end" : "center"};">
        ${hasImage ? html`<div class="hero-img-overlay">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" opacity=".4">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <circle cx="8.5" cy="8.5" r="1.5"/>
            <polyline points="21 15 16 10 5 21"/>
          </svg>
        </div>` : ""}
        <div class="hero-content" style="align-items:${hAlignCss};text-align:${hAlign}">
          <div class="badge">${tag.toUpperCase()} · Hero</div>
          <div class="headline ${tag}">${headline}</div>
          ${subHeadline ? html`<div class="sub">${subHeadline}</div>` : ""}
        </div>
        ${c.backgroundSize ? html`<div class="meta-badge">${c.backgroundSize}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .hero {
      position: relative; min-height: 100px; display: flex;
      justify-content: center; padding: 20px 16px; border-radius: 4px;
      overflow: hidden;
    }
    .hero-img-overlay {
      position: absolute; inset: 0; display: flex; align-items: center;
      justify-content: center; color: #fff;
    }
    .hero-content { position: relative; display: flex; flex-direction: column; gap: 6px; max-width: 600px; }
    .badge {
      font-size: 10px; font-weight: 700; letter-spacing: .06em;
      opacity: .7; text-transform: uppercase;
    }
    .headline { font-weight: 700; margin: 0; }
    .h1 { font-size: 2em; } .h2 { font-size: 1.6em; } .h3 { font-size: 1.3em; }
    .h4 { font-size: 1.1em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .sub { font-size: 13px; opacity: .85; }
    .meta-badge {
      position: absolute; top: 6px; right: 8px; font-size: 10px; padding: 2px 6px;
      border-radius: 3px; background: rgba(255,255,255,.2); color: #fff;
    }
  `];
}

customElements.define("dcms-hero-block-view", DcmsHeroBlockView);
