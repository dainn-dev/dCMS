import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS  = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";

const DEFAULT = {
  background: "#ffffff",
  colorPalette: ["#000000", "#5b5b5b", "#bcbcbc", "#f3f6f4", "#ffffff", "#2986cc", "#174a71"],
  savedFonts: [],
};

function parseValue(raw) {
  try {
    const v = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (v && typeof v === "object") {
      return {
        background:   v.background   ?? DEFAULT.background,
        colorPalette: Array.isArray(v.colorPalette) ? v.colorPalette : DEFAULT.colorPalette,
        savedFonts:   Array.isArray(v.savedFonts)   ? v.savedFonts   : DEFAULT.savedFonts,
      };
    }
  } catch { /* fall through */ }
  return { ...DEFAULT, colorPalette: [...DEFAULT.colorPalette], savedFonts: [] };
}

function norm6(raw) {
  const s = String(raw ?? "").replace(/^#/, "").slice(0, 6).toLowerCase();
  return s.length === 6 ? "#" + s : "#ffffff";
}

let _acpPromise = null;
function loadACP() {
  if (typeof window !== "undefined" && window.AColorPicker) return Promise.resolve();
  if (!_acpPromise) {
    _acpPromise = new Promise((resolve, reject) => {
      if (document.querySelector(`script[src="${ACP_JS}"]`)) return resolve();
      if (!document.querySelector(`link[href="${ACP_CSS}"]`)) {
        document.head.appendChild(
          Object.assign(document.createElement("link"), { rel: "stylesheet", href: ACP_CSS })
        );
      }
      const s = Object.assign(document.createElement("script"), { src: ACP_JS });
      s.onload = resolve; s.onerror = reject;
      document.head.appendChild(s);
    });
  }
  return _acpPromise;
}

export default class DcmsDesignBackgroundBlockPropertyEditor extends LitElement {
  static properties = {
    value:     { type: String },
    _cv:       { state: true },
    _open:     { state: true },
    _acpReady: { state: true },
  };

  #pickerEl = null;
  #picker   = null;

  constructor() {
    super();
    this.value     = undefined;
    this._cv       = { ...DEFAULT, colorPalette: [...DEFAULT.colorPalette], savedFonts: [] };
    this._open     = false;
    this._acpReady = false;
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

  #togglePicker() {
    if (this._open) {
      this._open = false;
      this.#destroyPicker();
    } else {
      this._open = true;
      this.updateComplete.then(() => this.#mountPicker());
    }
  }

  #mountPicker() {
    if (!this._acpReady || !window.AColorPicker) return;
    this.#destroyPicker();
    this.#pickerEl = this.shadowRoot.querySelector("[data-acp]");
    if (!this.#pickerEl) return;

    if (!this.shadowRoot.querySelector("link#dcms-bgb-css")) {
      this.shadowRoot.appendChild(
        Object.assign(document.createElement("link"), { id: "dcms-bgb-css", rel: "stylesheet", href: ACP_CSS })
      );
    }

    this.#picker = window.AColorPicker.createPicker(this.#pickerEl, {
      color: norm6(this._cv.background),
      showAlpha: false,
    });
    this.#picker.on("change", (_, c) => {
      this._cv = { ...this._cv, background: norm6(c.rgbhex) };
      this.#commit();
      this.requestUpdate();
    });
  }

  #destroyPicker() {
    if (this.#pickerEl) { this.#pickerEl.innerHTML = ""; }
    this.#pickerEl = null;
    this.#picker   = null;
  }

  #onHexInput(e) {
    const raw = e.target.value.trim();
    if (!/^#[0-9a-fA-F]{6}$/.test(raw)) return;
    this._cv = { ...this._cv, background: raw.toLowerCase() };
    this.#commit();
    this.#picker?.setColor?.(raw);
  }

  #commit() {
    const next = JSON.stringify(this._cv);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  render() {
    const bg = norm6(this._cv.background);
    const palette = Array.isArray(this._cv.colorPalette) ? this._cv.colorPalette : [];
    return html`
      <div class="wrap">
        <div class="label-row">
          <span class="field-label">Background Color</span>
        </div>
        ${palette.length > 0 ? html`
          <div class="palette">
            ${palette.map(c => html`
              <div class="pal-swatch ${norm6(c) === bg ? "pal-active" : ""}"
                style="background:${norm6(c)}" title="${norm6(c)}"
                @click=${() => this.#pickFromPalette(c)}>
              </div>
            `)}
          </div>
        ` : ""}
        <div class="color-row">
          <div class="swatch" style="background:${bg}"
            @click=${() => this.#togglePicker()}>
          </div>
          <input class="hex-input" type="text" maxlength="7"
            .value=${bg}
            @change=${this.#onHexInput}
            placeholder="#ffffff" />
          <button class="pick-btn" @click=${() => this.#togglePicker()}>
            ${this._open ? "Close" : "Pick"}
          </button>
        </div>
        ${this._open ? html`<div data-acp class="acp-host"></div>` : ""}
      </div>
    `;
  }

  #pickFromPalette(color) {
    const hex = norm6(color);
    this._cv = { ...this._cv, background: hex };
    this.#commit();
    if (this._open && this.#picker) {
      this.#picker.setColor?.(hex);
    }
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-direction: column; gap: 8px; max-width: 320px; }
    .label-row { display: flex; align-items: center; gap: 6px; }
    .field-label { font-size: 13px; font-weight: 600; }
    .color-row { display: flex; align-items: center; gap: 8px; }
    .swatch {
      width: 36px; height: 36px; border-radius: 4px; cursor: pointer; flex-shrink: 0;
      border: 1px solid var(--uui-color-border, #e3e3e3);
      box-shadow: 0 1px 3px rgba(0,0,0,.1);
    }
    .swatch:hover { box-shadow: 0 0 0 2px var(--uui-color-current, #3544b1); }
    .hex-input {
      width: 90px; padding: 6px 8px; font-size: 13px; font-family: monospace;
      border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 4px;
    }
    .hex-input:focus { outline: none; border-color: var(--uui-color-current, #3544b1); }
    .pick-btn {
      padding: 6px 12px; font-size: 12px; border-radius: 4px; cursor: pointer;
      border: 1px solid var(--uui-color-border, #e3e3e3); background: #fff;
    }
    .pick-btn:hover { background: var(--uui-color-surface-alt, #f5f5f5); }
    .palette { display: flex; flex-wrap: wrap; gap: 5px; margin-bottom: 4px; }
    .pal-swatch {
      width: 24px; height: 24px; border-radius: 3px; cursor: pointer;
      border: 2px solid transparent; box-shadow: 0 0 0 1px rgba(0,0,0,.15);
      transition: box-shadow .12s;
    }
    .pal-swatch:hover { box-shadow: 0 0 0 2px var(--uui-color-current, #3544b1); }
    .pal-swatch.pal-active {
      border-color: var(--uui-color-current, #3544b1);
      box-shadow: 0 0 0 3px rgba(53,68,177,.25);
    }
    .acp-host { margin-top: 4px; }
  `];
}

customElements.define("dcms-design-background-block-property-editor", DcmsDesignBackgroundBlockPropertyEditor);
