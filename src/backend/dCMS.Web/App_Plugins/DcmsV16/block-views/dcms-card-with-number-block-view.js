import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsCardWithNumberBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h5");
    const headline = c.headline || c.headlineText || c.title || "";
    const markup = c.richText?.markup || "";
    const number = c.number ?? c.cardNumber ?? null;
    const linkName = Array.isArray(c.link) ? c.link[0]?.name : null;
    const variant = c.variant || "Primary";
    // Corner radius from CornerRadius property (mirrors design.cornerRadius.cards)
    const cornerRadius = c.cornerRadius ?? null;
    const cardStyle = cornerRadius !== null ? `border-radius:${cornerRadius}px;` : "";

    // Drop shadow from DropShadow tokens (simplified — base class doesn't expose dropShadow directly)
    const shadow = scheme ? "" : "box-shadow: 0 2px 8px rgba(0,0,0,0.08);";

    return html`
      <div class="card" style="${schemeStyle}${cardStyle}${shadow}">
        ${number !== null ? html`
          <div class="card-number">${number}</div>
        ` : ""}
        <div class="card-body">
          ${headline ? html`<div class="headline ${tag}">${headline}</div>` : ""}
          ${markup ? html`<div class="text" .innerHTML=${markup.slice(0, 200)}></div>` : ""}
          ${c.additionalFeatures?.contentData?.length ? html`
            <ul class="features">
              ${c.additionalFeatures.contentData.slice(0, 4).map(f => html`
                <li>${f.text || f.headline || f.title || "—"}</li>
              `)}
            </ul>
          ` : ""}
          ${linkName ? html`
            <div class="btn-preview ${variant === "Secondary" ? "sec" : "pri"}">${linkName}</div>
          ` : ""}
        </div>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .card {
      border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 6px;
      overflow: hidden; display: flex; flex-direction: column;
    }
    .card-number {
      display: flex; align-items: center; justify-content: center;
      min-height: 48px; background: var(--block-btn-bg,#2986cc);
      color: var(--block-btn-color,#fff); font-size: 22px; font-weight: 700;
    }
    .card-body { padding: 10px 12px; display: flex; flex-direction: column; gap: 6px; }
    .headline { font-weight: 700; }
    .h1 { font-size: 1.8em; } .h2 { font-size: 1.4em; } .h3 { font-size: 1.2em; }
    .h4 { font-size: 1.05em; } .h5 { font-size: 1em; } .h6 { font-size: .9em; }
    .text { font-size: 12px; line-height: 1.5; max-height: 60px; overflow: hidden; }
    .features { margin: 0; padding-left: 16px; font-size: 11px; color: var(--uui-color-text,#333); }
    .features li { margin-bottom: 2px; }
    .btn-preview {
      display: inline-block; padding: 4px 10px; border-radius: 4px; font-size: 11px;
      font-weight: 600; align-self: flex-start;
    }
    .btn-preview.pri { background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff); }
    .btn-preview.sec { background: transparent; border: 1px solid var(--block-btn-border,#2986cc); color: var(--block-btn-border,#2986cc); }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; padding: 4px 12px 6px; }
  `];
}

customElements.define("dcms-card-with-number-block-view", DcmsCardWithNumberBlockView);
