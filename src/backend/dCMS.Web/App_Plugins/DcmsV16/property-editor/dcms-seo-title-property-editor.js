import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const OPT_MIN = 50;
const OPT_MAX = 60;

export default class DcmsSeoTitlePropertyEditor extends LitElement {
  static properties = {
    value:  { type: String },
    config: { type: Object },
  };

  constructor() {
    super();
    this.value  = "";
    this.config = {};
  }

  get #maxChars() {
    return Number(this.config?.maxChars) || 70;
  }

  #onChange(e) {
    const next = e.target.value.slice(0, this.#maxChars);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
    this.requestUpdate();
  }

  #status(len) {
    if (len === 0)                    return "empty";
    if (len >= OPT_MIN && len <= OPT_MAX) return "good";
    return "warn";
  }

  render() {
    const val  = this.value ?? "";
    const len  = val.length;
    const max  = this.#maxChars;
    const st   = this.#status(len);
    return html`
      <div class="wrap">
        <input type="text" class="inp" maxlength=${max} .value=${val}
          @input=${this.#onChange} placeholder="Enter SEO title…" />
        <div class="meta">
          <span class="hint">Optimal: ${OPT_MIN}–${OPT_MAX} characters</span>
          <span class="counter counter--${st}">${len} / ${max}</span>
        </div>
        ${this.#bar(len, max, st)}
        ${this.#msg(len, st)}
      </div>
    `;
  }

  #bar(len, max, st) {
    const pct = Math.min(100, Math.round((len / max) * 100));
    return html`<div class="bar-bg"><div class="bar-fill bar-fill--${st}" style="width:${pct}%"></div></div>`;
  }

  #msg(len, st) {
    if (st === "empty") return html`<p class="msg msg--warn">SEO title is empty.</p>`;
    if (st === "good")  return html`<p class="msg msg--good">Length is optimal.</p>`;
    if (len < OPT_MIN)  return html`<p class="msg msg--warn">Too short — add ${OPT_MIN - len} more character(s).</p>`;
    return html`<p class="msg msg--warn">Getting long — try to keep it under ${OPT_MAX} characters.</p>`;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-direction: column; gap: 6px; }
    .inp {
      width: 100%; box-sizing: border-box; padding: 8px 10px; font-size: 14px;
      border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 4px;
    }
    .inp:focus { outline: none; border-color: var(--uui-color-current, #3544b1); }
    .meta { display: flex; justify-content: space-between; align-items: center; }
    .hint { font-size: 12px; color: var(--uui-color-text-alt, #767676); }
    .counter { font-size: 12px; font-weight: 600; }
    .counter--empty { color: var(--uui-color-text-alt, #767676); }
    .counter--good  { color: #16a34a; }
    .counter--warn  { color: #d97706; }
    .bar-bg { height: 4px; background: #e5e7eb; border-radius: 2px; overflow: hidden; }
    .bar-fill { height: 100%; border-radius: 2px; transition: width .2s; }
    .bar-fill--empty { background: #d1d5db; }
    .bar-fill--good  { background: #16a34a; }
    .bar-fill--warn  { background: #d97706; }
    .msg { margin: 0; font-size: 12px; }
    .msg--good { color: #16a34a; }
    .msg--warn { color: #d97706; }
  `];
}

customElements.define("dcms-seo-title-property-editor", DcmsSeoTitlePropertyEditor);
