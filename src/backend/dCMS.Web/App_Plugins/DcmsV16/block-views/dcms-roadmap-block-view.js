import { html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { DcmsBlockBase, headlineTag } from "./dcms-block-base.js";

export default class DcmsRoadmapBlockView extends DcmsBlockBase {
  render() {
    const c = this.content || {};
    const scheme = this._resolveScheme(c.backgroundColor);
    const schemeStyle = scheme ? this._schemeCssVars(scheme) : "";
    const tag = headlineTag(c.headlineLevel, "h2");
    const headline = c.headline || c.headlineText || "";
    const subheadline = c.subheadline || c.subtitle || "";
    const steps = c.steps?.contentData || c.items?.contentData || c.milestones?.contentData || [];

    return html`
      <div class="wrap" style="${schemeStyle}">
        ${headline ? html`<div class="section-headline ${tag}">${headline}</div>` : ""}
        ${subheadline ? html`<div class="sub">${subheadline}</div>` : ""}
        ${steps.length === 0 ? html`<p class="empty">(no roadmap steps)</p>` : html`
          <div class="timeline">
            ${steps.map((step, i) => {
              const status = step.status || step.state || "";
              const isDone = /done|complete|finished/i.test(status);
              const isActive = /active|current|in.progress/i.test(status);
              const stepTitle = step.headline || step.title || step.name || `Step ${i + 1}`;
              const stepDate = step.date || step.quarter || step.period || null;
              const stepText = step.richText?.markup || step.description || "";

              return html`
                <div class="step ${isDone ? 'done' : isActive ? 'active' : ''}">
                  <div class="step-marker">
                    <div class="dot">${isDone ? "✓" : i + 1}</div>
                    ${i < steps.length - 1 ? html`<div class="line"></div>` : ""}
                  </div>
                  <div class="step-content">
                    <div class="step-title">${stepTitle}</div>
                    ${stepDate ? html`<div class="step-date">${stepDate}</div>` : ""}
                    ${stepText ? html`<div class="step-text" .innerHTML=${stepText.slice(0, 120)}></div>` : ""}
                  </div>
                </div>
              `;
            })}
          </div>
        `}
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
    .timeline { display: flex; flex-direction: column; gap: 0; }
    .step { display: flex; gap: 10px; }
    .step-marker { display: flex; flex-direction: column; align-items: center; width: 24px; flex-shrink: 0; }
    .dot {
      width: 24px; height: 24px; border-radius: 50%; flex-shrink: 0;
      display: flex; align-items: center; justify-content: center;
      font-size: 10px; font-weight: 700;
      border: 2px solid var(--uui-color-border,#d0d0d0);
      background: var(--uui-color-surface,#fff);
      color: var(--uui-color-text-alt,#767676);
    }
    .step.done .dot {
      background: var(--block-btn-bg,#2986cc); border-color: var(--block-btn-bg,#2986cc);
      color: var(--block-btn-color,#fff);
    }
    .step.active .dot {
      border-color: var(--block-btn-bg,#2986cc); color: var(--block-btn-bg,#2986cc); font-weight: 800;
    }
    .line { flex: 1; width: 2px; background: var(--uui-color-border,#e3e3e3); min-height: 16px; margin: 2px 0; }
    .step.done .line { background: var(--block-btn-bg,#2986cc); }
    .step-content { padding: 2px 0 14px; flex: 1; }
    .step-title { font-size: 12px; font-weight: 700; }
    .step.active .step-title { color: var(--block-btn-bg,#2986cc); }
    .step-date { font-size: 10px; color: var(--uui-color-text-alt,#767676); margin-top: 1px; }
    .step-text { font-size: 11px; line-height: 1.4; margin-top: 3px; max-height: 36px; overflow: hidden; color: var(--uui-color-text,#444); }
    .empty { font-size: 12px; color: var(--uui-color-text-alt,#767676); font-style: italic; }
    .css-badge { font-size: 10px; color: var(--uui-color-text-alt,#767676); font-style: italic; margin-top: 4px; }
  `];
}

customElements.define("dcms-roadmap-block-view", DcmsRoadmapBlockView);
