import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS  = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";
const IMG     = "/App_Plugins/UmbracoBlockGrid/GlobalDesign/img/";

const SECTIONS = [
  {
    key: "picturesAndVideo",
    label: "Pictures & Video",
    previews: [IMG + "preview-img.jpg", IMG + "preview-video.jpg"],
    type: "img",
  },
  {
    key: "cards",
    label: "Cards",
    previews: [IMG + "card-01.svg", IMG + "card-03.svg", IMG + "card-02.svg"],
    type: "card",
  },
];

const DEFAULT_SEC = { offsetX: 0, offsetY: 0, blurRadius: 0, color: "5b5b5b", opacity: 0 };
const DEFAULT = {
  picturesAndVideo: { ...DEFAULT_SEC },
  cards: { ...DEFAULT_SEC, color: "bcbcbc" },
};

function parseValue(raw) {
  try {
    const v = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (v && typeof v === "object") {
      return {
        picturesAndVideo: { ...DEFAULT.picturesAndVideo, ...v.picturesAndVideo },
        cards: { ...DEFAULT.cards, ...v.cards },
      };
    }
  } catch { /* fall through */ }
  return { picturesAndVideo: { ...DEFAULT.picturesAndVideo }, cards: { ...DEFAULT.cards } };
}

function hexToRgb(hex) {
  const h = hex.replace("#", "");
  const n = parseInt(h.length === 3 ? h.split("").map(c => c + c).join("") : h, 16);
  return `${(n >> 16) & 255},${(n >> 8) & 255},${n & 255}`;
}

function boxShadow(sec) {
  const { offsetX, offsetY, blurRadius, color, opacity } = sec;
  return `${offsetX}px ${offsetY}px ${blurRadius}px rgba(${hexToRgb(color)},${opacity / 100})`;
}

let _acpPromise = null;
function loadACP() {
  if (typeof window !== "undefined" && window.AColorPicker) return Promise.resolve();
  if (!_acpPromise) {
    _acpPromise = new Promise((resolve, reject) => {
      if (document.querySelector(`script[src="${ACP_JS}"]`)) { return resolve(); }
      if (!document.querySelector(`link[href="${ACP_CSS}"]`)) {
        const lnk = Object.assign(document.createElement("link"), { rel: "stylesheet", href: ACP_CSS });
        document.head.appendChild(lnk);
      }
      const s = Object.assign(document.createElement("script"), { src: ACP_JS });
      s.onload = resolve; s.onerror = reject;
      document.head.appendChild(s);
    });
  }
  return _acpPromise;
}

export default class DcmsDropShadowPropertyEditor extends LitElement {
  static properties = {
    value:       { type: String },
    _cv:         { state: true },
    _open:       { state: true },
    _acpReady:   { state: true },
    _active:     { state: true }, // { secKey, label }
  };

  #pickerEl = null;
  #picker    = null;

  constructor() {
    super();
    this.value     = undefined;
    this._cv       = { picturesAndVideo: { ...DEFAULT.picturesAndVideo }, cards: { ...DEFAULT.cards } };
    this._open     = new Set();
    this._acpReady = false;
    this._active   = null;
  }

  connectedCallback() {
    super.connectedCallback();
    this._cv = parseValue(this.value);
    loadACP().then(() => { this._acpReady = true; });
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#destroyPicker();
  }

  willUpdate(changed) {
    if (changed.has("value")) this._cv = parseValue(this.value);
  }

  #toggle(key) {
    const s = new Set(this._open);
    s.has(key) ? s.delete(key) : s.add(key);
    this._open = s;
    if (!s.has(key) && this._active?.secKey === key) {
      this.#destroyPicker();
      this._active = null;
    }
  }

  #setNum(secKey, field, val) {
    const n = Math.max(0, parseInt(val) || 0);
    this._cv = { ...this._cv, [secKey]: { ...this._cv[secKey], [field]: n } };
    this.#commit();
  }

  #openPicker(secKey) {
    if (!this._acpReady || !window.AColorPicker) return;
    this.#destroyPicker();
    this._active = { secKey };
    this.updateComplete.then(() => {
      this.#pickerEl = this.shadowRoot.querySelector(`[data-acp="${secKey}"]`);
      if (!this.#pickerEl) return;
      this.#picker = window.AColorPicker.createPicker(this.#pickerEl, {
        color: "#" + this._cv[secKey].color,
        showAlpha: false,
      });
      this.#picker.on("change", (_, c) => {
        const hex = c.rgbhex.replace("#", "");
        this._cv = { ...this._cv, [secKey]: { ...this._cv[secKey], color: hex } };
        this.#commit();
        this.requestUpdate();
      });
    });
  }

  #destroyPicker() {
    if (this.#pickerEl) {
      this.#pickerEl.innerHTML = "";
      this.#pickerEl = null;
      this.#picker = null;
    }
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
    const data = this._cv[sec.key];
    return html`
      <div class="sec">
        <div class="sec-head" @click=${() => this.#toggle(sec.key)}>
          <span>${sec.label}</span>
          <span class="chev">${open ? "▲" : "▼"}</span>
        </div>
        ${open ? this.#body(sec, data) : ""}
      </div>
    `;
  }

  #body(sec, data) {
    const bs  = boxShadow(data);
    const isActive = this._active?.secKey === sec.key;
    return html`
      <div class="sec-body">
        <div class="ctrl">
          <div class="field">
            <label>Color</label>
            <div class="color-row">
              <div class="swatch" style="background:#${data.color}"
                @click=${() => isActive ? (this.#destroyPicker(), this._active = null) : this.#openPicker(sec.key)}>
              </div>
              <span class="hex-val">#${data.color}</span>
            </div>
            ${isActive ? html`<div data-acp="${sec.key}" class="acp-host"></div>` : ""}
          </div>
          ${[
            { field: "opacity", label: "Opacity", unit: "%", min: 0, max: 100 },
            { field: "offsetX", label: "Offset-X", unit: "px", min: 0 },
            { field: "offsetY", label: "Offset-Y", unit: "px", min: 0 },
            { field: "blurRadius", label: "Blur Radius", unit: "px", min: 0 },
          ].map(f => html`
            <div class="field">
              <label>${f.label}</label>
              <div class="num-wrap">
                <input type="number" min=${f.min ?? 0} ${f.max != null ? `max="${f.max}"` : ""}
                  .value=${String(data[f.field])}
                  @change=${e => this.#setNum(sec.key, f.field, e.target.value)} />
                <span class="unit">${f.unit}</span>
              </div>
            </div>
          `)}
        </div>
        <div class="preview-area">
          <div class="imgs" style="background:#fff;padding:12px;border-radius:4px;">
            ${sec.previews.map(src => html`
              <img src=${src} style="box-shadow:${bs}${sec.type === "card" ? ";border:1px solid #caced5" : ""}" />
            `)}
          </div>
        </div>
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
    .ctrl { display: flex; flex-direction: column; gap: 10px; min-width: 180px; flex-shrink: 0; }
    .field { display: flex; flex-direction: column; gap: 4px; }
    .field label { font-size: 12px; font-weight: 600; }
    .color-row { display: flex; align-items: center; gap: 8px; }
    .swatch {
      width: 28px; height: 28px; border-radius: 4px; cursor: pointer;
      border: 1px solid var(--uui-color-border, #e3e3e3); flex-shrink: 0;
    }
    .hex-val { font-size: 12px; font-family: monospace; }
    .acp-host { margin-top: 4px; }
    .num-wrap { display: flex; align-items: center; gap: 4px; }
    .num-wrap input {
      width: 72px; padding: 5px 8px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; font-size: 13px;
    }
    .unit { font-size: 12px; color: var(--uui-color-text-alt, #767676); }
    .preview-area { flex: 1; background: #d9d9d9; padding: 12px; border-radius: 4px; }
    .imgs { display: flex; gap: 12px; flex-wrap: wrap; }
    .imgs img { height: 80px; object-fit: cover; }
  `];
}

customElements.define("dcms-drop-shadow-property-editor", DcmsDropShadowPropertyEditor);
