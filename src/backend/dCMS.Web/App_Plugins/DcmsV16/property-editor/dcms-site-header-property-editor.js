import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const IMG_BASE = "/App_Plugins/DcmsV16/img/";

const HEADERS = [
  {
    BackOfficeImage: IMG_BASE + "Header_Option_1.jpg",
    BackOfficeDescription: "Logo on the left side, links, and buttons on the right side",
    RazorView: "header1",
  },
  {
    BackOfficeImage: IMG_BASE + "Header_Option_2.jpg",
    BackOfficeDescription: "Logo on the left side, links centered, buttons on the right side",
    RazorView: "header2",
  },
  {
    BackOfficeImage: IMG_BASE + "Header_Option_3.jpg",
    BackOfficeDescription: "Logo and links on the left, buttons on the right side",
    RazorView: "header3",
  },
];

const INFO_SVG = html`<svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" viewBox="0 0 16 16">
  <path d="M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14m0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16"/>
  <path d="m8.93 6.588-2.29.287-.082.38.45.083c.294.07.352.176.288.469l-.738 3.468c-.194.897.105 1.319.808 1.319.545 0 1.178-.252 1.465-.598l.088-.416c-.2.176-.492.246-.686.246-.275 0-.375-.193-.304-.533zM9 4.5a1 1 0 1 1-2 0 1 1 0 0 1 2 0"/>
</svg>`;

function parseValue(raw) {
  try {
    const v = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (v?.RazorView) return v;
  } catch { /* fall through */ }
  return { ...HEADERS[0] };
}

export default class DcmsSiteHeaderPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _sel: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this._sel = 0;
  }

  connectedCallback() {
    super.connectedCallback();
    this._sel = this.#indexFromValue(this.value);
    if (!this.value) this.#emit(0);
  }

  willUpdate(changed) {
    if (changed.has("value")) {
      this._sel = this.#indexFromValue(this.value);
    }
  }

  #indexFromValue(raw) {
    const v = parseValue(raw);
    const i = HEADERS.findIndex(h => h.RazorView === v.RazorView);
    return i === -1 ? 0 : i;
  }

  #emit(idx) {
    const next = JSON.stringify(HEADERS[idx]);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  #select(idx) {
    this._sel = idx;
    this.#emit(idx);
  }

  render() {
    const preview = HEADERS[this._sel] ?? HEADERS[0];
    return html`
      <div class="layout">
        <ul class="list">
          ${HEADERS.map((h, i) => html`
            <li class="item ${i === this._sel ? "active" : ""}" @click=${() => this.#select(i)}>
              <input type="radio" name="header-sel" .checked=${i === this._sel}
                @change=${() => this.#select(i)} />
              <span class="lbl">Header ${i + 1}</span>
              <span class="info" title=${h.BackOfficeDescription}>${INFO_SVG}</span>
            </li>
          `)}
        </ul>
        <div class="preview">
          <img src=${preview.BackOfficeImage} alt="Header preview" />
          <p class="desc">${preview.BackOfficeDescription}</p>
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
    .desc { font-size: 12px; color: var(--uui-color-text-alt, #767676); margin: 6px 0 0; font-style: italic; }
  `];
}

customElements.define("dcms-site-header-property-editor", DcmsSiteHeaderPropertyEditor);
