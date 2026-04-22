import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const IMG = "/App_Plugins/DcmsV16/img/";

const SECTIONS = [
  {
    key: "picturesAndVideo",
    label: "Pictures & Video",
    previews: [
      { src: IMG + "preview-img.jpg" },
      { src: IMG + "preview-video.jpg" },
    ],
    type: "img",
  },
  {
    key: "buttons",
    label: "Buttons",
    type: "btn",
  },
  {
    key: "cards",
    label: "Cards",
    previews: [
      { src: IMG + "card-01.svg", bordered: true },
      { src: IMG + "card-03.svg", bordered: true },
      { src: IMG + "card-02.svg", bordered: true },
    ],
    type: "img",
  },
];

const DEFAULT = { picturesAndVideo: 0, buttons: 8, cards: 0 };

function parseValue(raw) {
  try {
    const v = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (v && typeof v === "object") return { ...DEFAULT, ...v };
  } catch { /* fall through */ }
  return { ...DEFAULT };
}

export default class DcmsCornerRadiusPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _cv:   { state: true },
    _open: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this._cv   = { ...DEFAULT };
    this._open = new Set();
  }

  connectedCallback() {
    super.connectedCallback();
    this._cv = parseValue(this.value);
  }

  willUpdate(changed) {
    if (changed.has("value")) this._cv = parseValue(this.value);
  }

  #toggle(key) {
    const s = new Set(this._open);
    s.has(key) ? s.delete(key) : s.add(key);
    this._open = s;
  }

  #set(key, val) {
    const n = Math.max(0, Math.min(999, parseInt(val) || 0));
    this._cv = { ...this._cv, [key]: n };
    this.#commit();
  }

  #commit() {
    const next = JSON.stringify(this._cv);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  render() {
    return html`<div class="editor">${SECTIONS.map(s => this.#section(s))}</div>`;
  }

  #section(sec) {
    const open = this._open.has(sec.key);
    const val  = this._cv[sec.key] ?? 0;
    return html`
      <div class="sec">
        <div class="sec-head" @click=${() => this.#toggle(sec.key)}>
          <span>${sec.label}</span>
          <span class="chev">${open ? "▲" : "▼"}</span>
        </div>
        ${open ? this.#body(sec, val) : ""}
      </div>
    `;
  }

  #body(sec, val) {
    return html`
      <div class="sec-body">
        <div class="ctrl">
          <label>Border Radius</label>
          <div class="num-wrap">
            <input type="number" min="0" max="999" .value=${String(val)}
              @change=${e => this.#set(sec.key, e.target.value)} />
            <span class="unit">px</span>
          </div>
        </div>
        <div class="preview-area">
          ${sec.type === "img" ? this.#imgPreview(sec, val) : this.#btnPreview(val)}
        </div>
      </div>
    `;
  }

  #imgPreview(sec, val) {
    return html`
      <div class="imgs">
        ${sec.previews.map(p => html`
          <img src=${p.src}
            style="border-radius:${val}px${p.bordered ? ";border:1px solid #caced5" : ""}" />
        `)}
      </div>
    `;
  }

  #btnPreview(val) {
    const r = `border-radius:${val}px`;
    return html`
      <div class="btns">
        <button class="btn btn-lg" style=${r}>Large Button</button>
        <button class="btn" style=${r}>Normal Button</button>
        <button class="btn btn-sm" style=${r}>Small Button</button>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .editor { display: flex; flex-direction: column; gap: 4px; }
    .sec { border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 6px; overflow: hidden; }
    .sec-head {
      display: flex; justify-content: space-between; align-items: center;
      padding: 10px 14px; cursor: pointer; background: var(--uui-color-surface, #fff);
      user-select: none; font-weight: 600;
    }
    .sec-head:hover { background: var(--uui-color-surface-alt, #f5f5f5); }
    .chev { font-size: 10px; color: var(--uui-color-text-alt, #767676); }
    .sec-body {
      display: flex; gap: 16px; padding: 14px;
      border-top: 1px solid var(--uui-color-border, #e3e3e3);
      background: var(--uui-color-surface-alt, #f5f5f5);
    }
    .ctrl { display: flex; flex-direction: column; gap: 6px; min-width: 160px; flex-shrink: 0; }
    .ctrl label { font-size: 12px; font-weight: 600; }
    .num-wrap { display: flex; align-items: center; gap: 4px; }
    .num-wrap input {
      width: 72px; padding: 6px 8px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; font-size: 14px;
    }
    .unit { font-size: 12px; color: var(--uui-color-text-alt, #767676); }
    .preview-area { flex: 1; background: #d9d9d9; padding: 12px; border-radius: 4px; }
    .imgs { display: flex; gap: 12px; flex-wrap: wrap; background: #fff; padding: 12px; border-radius: 4px; }
    .imgs img { height: 80px; object-fit: cover; }
    .btns { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; background: #fff; padding: 12px; border-radius: 4px; }
    .btn {
      padding: 8px 16px; background: #3544b1; color: #fff; border: none;
      cursor: default; font-size: 14px; font-weight: 500;
    }
    .btn-lg { padding: 10px 20px; font-size: 16px; }
    .btn-sm { padding: 5px 10px; font-size: 12px; }
  `];
}

customElements.define("dcms-corner-radius-property-editor", DcmsCornerRadiusPropertyEditor);
