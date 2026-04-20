import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsContactUsFormBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h2");
    const headline = c.headline || c.headlineText || c.title || "";
    const subheadline = c.subheadline || c.subtitle || "";
    // fields is a nested block list; each item has a contentTypeKey → alias determined at runtime
    // In backoffice preview we just show placeholders for each field
    const fields = c.fields?.contentData || [];
    const submitLabel = c.submitLabel || c.submitText || "Send Message";
    const variant = c.variant || "Primary";

    // Common field types to display
    const fieldLabels = fields.length > 0
      ? fields.map((f, i) => f.label || f.name || f.placeholder || `Field ${i + 1}`)
      : ["Full Name", "Email Address", "Message"];

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="section-headline ${tag}">${headline}</div>` : ""}
        ${subheadline ? html`<div class="sub">${subheadline}</div>` : ""}
        <div class="form">
          ${fieldLabels.map((label, i) => html`
            <div class="field">
              <div class="label">${label}</div>
              ${i === fieldLabels.length - 1 && fieldLabels.length <= 4
                ? html`<div class="textarea-preview"></div>`
                : html`<div class="input-preview"></div>`}
            </div>
          `)}
          <div class="btn-wrap">
            <div class="btn-preview ${variant === 'Secondary' ? 'sec' : 'pri'}">${submitLabel}</div>
          </div>
        </div>
        ${c.customCssClasses ? html`<div class="css-badge">${c.customCssClasses}</div>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { padding: 12px; }
    .section-headline { font-weight: 700; margin-bottom: 4px; }
    .h1{font-size:1.8em} .h2{font-size:1.4em} .h3{font-size:1.2em}
    .h4{font-size:1.05em} .h5{font-size:1em} .h6{font-size:.9em}
    .sub { font-size: 12px; color: var(--uui-color-text-alt,#767676); margin-bottom: 10px; }
    .form { display: flex; flex-direction: column; gap: 8px; }
    .field { display: flex; flex-direction: column; gap: 3px; }
    .label { font-size: 11px; font-weight: 600; color: var(--uui-color-text,#333); }
    .input-preview {
      height: 28px; border-radius: 4px;
      border: 1px solid var(--uui-color-border,#d0d0d0);
      background: var(--uui-color-surface,#fff);
    }
    .textarea-preview {
      height: 64px; border-radius: 4px;
      border: 1px solid var(--uui-color-border,#d0d0d0);
      background: var(--uui-color-surface,#fff);
    }
    .btn-wrap { margin-top: 4px; }
    .btn-preview {
      display: inline-block; padding: 6px 18px; border-radius: 4px; font-size: 12px; font-weight: 600;
    }
    .btn-preview.pri { background: var(--block-btn-bg,#2986cc); color: var(--block-btn-color,#fff); }
    .btn-preview.sec { background: transparent; border: 1px solid var(--block-btn-border,#2986cc); color: var(--block-btn-border,#2986cc); }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 6px; }
  `];
}

customElements.define("dcms-contact-us-form-block-view", DcmsContactUsFormBlockView);
