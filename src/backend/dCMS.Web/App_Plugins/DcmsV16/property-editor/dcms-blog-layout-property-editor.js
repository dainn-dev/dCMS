import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const IMG_BASE = "/App_Plugins/DcmsV16/img/";
const DEFAULT_VALUE = "1";

const LAYOUTS = [
  { value: "0", label: "Layout 1", image: IMG_BASE + "Blog_Layout_1.jpg", description: "Option 1" },
  { value: "1", label: "Layout 2", image: IMG_BASE + "Blog_Layout_2.jpg", description: "Option 2" },
  { value: "2", label: "Layout 3", image: IMG_BASE + "Blog_Layout_3.jpg", description: "Option 3" },
  { value: "3", label: "Layout 4", image: IMG_BASE + "Blog_Layout_4.jpg", description: "Option 4" },
  { value: "4", label: "Layout 5", image: IMG_BASE + "Blog_Layout_5.jpg", description: "Option 5" },
];

const INFO_SVG = html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
  <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14m0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16"/>
  <path d="m8.93 6.588-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533zM9 4.5a1 1 0 1 1-2 0 1 1 0 0 1 2 0"/>
</svg>`;

export default class DcmsBlogLayoutPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _sel: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this._sel = DEFAULT_VALUE;
  }

  connectedCallback() {
    super.connectedCallback();
    const v = this.value ?? DEFAULT_VALUE;
    this._sel = LAYOUTS.some(l => l.value === v) ? v : DEFAULT_VALUE;
    if (!this.value) this.#emit(DEFAULT_VALUE);
  }

  willUpdate(changed) {
    if (changed.has("value") && this.value != null) {
      this._sel = LAYOUTS.some(l => l.value === this.value) ? this.value : DEFAULT_VALUE;
    }
  }

  #emit(val) {
    if (val !== this.value) {
      this.value = val;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  #select(val) {
    this._sel = val;
    this.#emit(val);
  }

  render() {
    const preview = LAYOUTS.find(l => l.value === this._sel) ?? LAYOUTS[1];
    return html`
      <div class="layout">
        <ul class="list">
          ${LAYOUTS.map(l => html`
            <li class="item ${l.value === this._sel ? "active" : ""}" @click=${() => this.#select(l.value)}>
              <input type="radio" name="blog-layout" .checked=${l.value === this._sel}
                @change=${() => this.#select(l.value)} />
              <span class="lbl">${l.label}</span>
              <span class="info" title=${l.description}>${INFO_SVG}</span>
            </li>
          `)}
        </ul>
        <div class="preview">
          <img src=${preview.image} alt="Blog layout preview" />
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .layout { display: flex; gap: 20px; align-items: flex-start; }
    .list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 6px; min-width: 160px; }
    .item {
      display: flex; align-items: center; gap: 8px; padding: 8px 12px;
      border: 2px solid var(--uui-color-border, #e3e3e3); border-radius: 6px;
      cursor: pointer; transition: border-color .15s;
    }
    .item:hover { border-color: var(--uui-color-current-emphasis, #5a67e8); }
    .item.active { border-color: var(--uui-color-current, #3544b1); background: var(--uui-color-current-surface, #f0f2ff); }
    .item input[type="radio"] { accent-color: var(--uui-color-current, #3544b1); flex-shrink: 0; }
    .lbl { flex: 1; font-size: 14px; }
    .info { color: var(--uui-color-text-alt, #767676); line-height: 0; }
    .preview { flex: 1; }
    .preview img { width: 100%; border-radius: 6px; border: 1px solid var(--uui-color-border, #e3e3e3); display: block; }
  `];
}

customElements.define("dcms-blog-layout-property-editor", DcmsBlogLayoutPropertyEditor);
