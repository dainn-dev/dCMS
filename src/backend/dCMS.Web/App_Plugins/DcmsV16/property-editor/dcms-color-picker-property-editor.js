import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";
const LS_PALETTE = "dcms.colorPicker.palette.v1";

const DEFAULT_PALETTE = ["#000000", "#5b5b5b", "#bcbcbc", "#f3f6f4", "#ffffff", "#2986cc", "#174a71"];
const FALLBACK_DEFAULT = "#9f6c31ff";
const PALETTE_CHANNEL = "dcms_color_palette";

/** @type {Promise<void> | null} */
let acpLoadPromise = null;
/** @type {BroadcastChannel | null} */
let _paletteChannel = null;

function getPaletteChannel() {
  if (!_paletteChannel && typeof BroadcastChannel !== "undefined") {
    _paletteChannel = new BroadcastChannel(PALETTE_CHANNEL);
  }
  return _paletteChannel;
}

function loadPalette() {
  try {
    const raw = localStorage.getItem(LS_PALETTE);
    if (!raw) return [...DEFAULT_PALETTE];
    const p = JSON.parse(raw);
    return Array.isArray(p) && p.length ? p : [...DEFAULT_PALETTE];
  } catch {
    return [...DEFAULT_PALETTE];
  }
}

function savePalette(/** @type {string[]} */ colors) {
  try {
    localStorage.setItem(LS_PALETTE, JSON.stringify(colors));
    getPaletteChannel()?.postMessage({ type: "update", colors });
  } catch {
    /* ignore */
  }
}

function ensureAColorPicker() {
  if (typeof window !== "undefined" && window.AColorPicker) return Promise.resolve();
  if (!acpLoadPromise) {
    acpLoadPromise = new Promise((resolve, reject) => {
      const existing = document.querySelector(`script[src="${ACP_JS}"]`);
      if (existing) {
        existing.addEventListener("load", () => resolve(), { once: true });
        existing.addEventListener("error", () => reject(new Error("acolorpicker load")), { once: true });
        return;
      }
      const s = document.createElement("script");
      s.src = ACP_JS;
      s.async = true;
      s.onload = () => resolve();
      s.onerror = () => reject(new Error("acolorpicker load"));
      document.head.appendChild(s);
    });
  }
  return acpLoadPromise;
}

export default class DcmsColorPickerPropertyEditor extends LitElement {
  static properties = {
    /** @type {string | undefined} */
    value: { type: String },
    /** Data type configuration (defaultColor, …) */
    config: { type: Object, attribute: false },
    _visible: { state: true },
    /** @type {string[]} */
    _palette: { state: true },
    /** @type {string[]} */
    _paletteCached: { state: true },
    _previousValue: { state: true },
    _libReady: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this.config = undefined;
    this._visible = false;
    this._palette = loadPalette();
    this._paletteCached = [...this._palette];
    this._previousValue = "";
    this._libReady = false;
    /** @type {any} */
    this._picker = null;
    this._pickerMounted = false;
    /** @type {(e: MouseEvent) => void} */
    this._onDocClick = this.#onDocClick.bind(this);
  }

  connectedCallback() {
    super.connectedCallback();
    this.#applyDefaultIfEmpty();
    ensureAColorPicker()
      .then(() => { this._libReady = true; })
      .catch(() => { this._libReady = false; });
    this._onPaletteSync = (/** @type {MessageEvent} */ e) => {
      if (e.data?.type === "update" && Array.isArray(e.data.colors)) {
        this._palette = [...e.data.colors];
        this._paletteCached = [...e.data.colors];
      }
    };
    getPaletteChannel()?.addEventListener("message", this._onPaletteSync);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener("click", this._onDocClick, true);
    getPaletteChannel()?.removeEventListener("message", this._onPaletteSync);
    this.#teardownPicker();
  }

  #getDefaultColor() {
    const cfg = this.config;
    if (!cfg) return FALLBACK_DEFAULT;
    if (typeof cfg.getValueByAlias === "function") {
      const v = cfg.getValueByAlias("defaultColor");
      if (v != null && String(v).trim() !== "") return String(v);
      return FALLBACK_DEFAULT;
    }
    if (Array.isArray(cfg)) {
      const row = cfg.find((/** @type {any} */ x) => x?.alias === "defaultColor");
      if (row?.value != null && String(row.value).trim() !== "") return String(row.value);
      return FALLBACK_DEFAULT;
    }
    if (typeof cfg === "object" && cfg.defaultColor != null && String(cfg.defaultColor).trim() !== "") {
      return String(cfg.defaultColor);
    }
    return FALLBACK_DEFAULT;
  }

  willUpdate(/** @type {Map<string, unknown>} */ changed) {
    super.willUpdate(changed);
    if (changed.has("config")) {
      this.#applyDefaultIfEmpty();
    }
  }

  #applyDefaultIfEmpty() {
    if (this.value == null || String(this.value).trim() === "") {
      this.value = this.#getDefaultColor();
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  #injectAcpCssOnce() {
    const id = "dcms-acp-css";
    if (this.shadowRoot?.querySelector(`link#${id}`)) return;
    const link = document.createElement("link");
    link.id = id;
    link.rel = "stylesheet";
    link.href = ACP_CSS;
    this.shadowRoot?.appendChild(link);
  }

  #teardownPicker() {
    this._pickerMounted = false;
    this._picker = null;
    const host = this.renderRoot?.querySelector("[data-color-picker]");
    if (host) host.innerHTML = "";
  }

  async #mountPicker() {
    if (this._pickerMounted || !this._visible) return;
    await ensureAColorPicker();
    if (!window.AColorPicker) return;
    this.#injectAcpCssOnce();
    const host = this.renderRoot.querySelector("[data-color-picker]");
    if (!host) return;

    const initial = this.value && String(this.value).trim() !== "" ? this.value : this.#getDefaultColor();
    host.setAttribute("acp-color", initial);
    host.setAttribute("acp-show-alpha", "");
    host.setAttribute("acp-show-rgb", "no");
    host.setAttribute("acp-show-hsl", "no");

    this._picker = window.AColorPicker.from(host, {
      hueBarSize: [252, 150],
      slBarSize: [252, 150],
    });

    this._picker.on("change", (/** @type {any} */ picker) => {
      const hex = picker?.all?.hexcss4;
      if (typeof hex === "string") {
        this.value = hex;
        this.dispatchEvent(new UmbChangeEvent());
      }
    });

    requestAnimationFrame(() => {
      const input = this.renderRoot.querySelector(".a-color-picker-single-input input");
      if (input) {
        input.addEventListener("keypress", (e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            const hex = /** @type {HTMLInputElement} */ (e.target).value;
            if (this._picker?.[0]) {
              this._picker[0].setColor(hex);
              this.value = hex;
              this.dispatchEvent(new UmbChangeEvent());
            }
          }
        });
      }
    });

    this._pickerMounted = true;
  }

  #onDocClick(/** @type {MouseEvent} */ event) {
    if (!this._visible) return;
    const path = event.composedPath();
    if (path.includes(this)) return;
    this.#closeAndCommitPalette();
  }

  #closeAndCommitPalette() {
    savePalette(this._palette);
    this._paletteCached = [...this._palette];
    this._visible = false;
    document.removeEventListener("click", this._onDocClick, true);
    this.#teardownPicker();
    this.requestUpdate();
  }

  #toggle(/** @type {string | undefined} */ action) {
    if (action === "cancel") {
      this.value = this._previousValue;
      this._palette = [...this._paletteCached];
      this.dispatchEvent(new UmbChangeEvent());
      this._visible = false;
      document.removeEventListener("click", this._onDocClick, true);
      this.#teardownPicker();
      this.requestUpdate();
      return;
    }
    if (action === "choose") {
      this.#closeAndCommitPalette();
      this.requestUpdate();
      return;
    }
    this._visible = !this._visible;
    if (this._visible) {
      this._previousValue = this.value ?? "";
      this._paletteCached = [...this._palette];
      queueMicrotask(() => document.addEventListener("click", this._onDocClick, true));
      this.updateComplete.then(() => this.#mountPicker());
    } else {
      document.removeEventListener("click", this._onDocClick, true);
      this.#teardownPicker();
      this.requestUpdate();
    }
  }

  #addColor(/** @type {Event} */ e) {
    e.preventDefault();
    if (!this._picker?.[0]) return;
    const hex = this._picker[0].all.hexcss4;
    if (!this._palette.includes(hex)) {
      this._palette = [...this._palette, hex];
    }
  }

  #deleteColor(/** @type {Event} */ e) {
    e.preventDefault();
    e.stopPropagation();
    const btn = /** @type {HTMLElement} */ (e.currentTarget);
    const block = btn.closest(".color-block");
    if (!block) return;
    const hex = block.getAttribute("data-color-hex");
    if (!hex) return;
    this._palette = this._palette.filter((c) => c !== hex);
  }

  #selectColor(/** @type {Event} */ e) {
    e.preventDefault();
    const el = /** @type {HTMLElement} */ (e.currentTarget);
    const hex = el.getAttribute("data-color-hex");
    if (!hex || !this._picker?.[0]) return;
    this.renderRoot.querySelectorAll("[data-color-hex]").forEach((node) => node.classList.remove("selected"));
    el.classList.add("selected");
    this._picker[0].setColor(hex);
    this.value = hex;
    this.dispatchEvent(new UmbChangeEvent());
  }

  static styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
        /* Umbraco property row can be narrow; inner-holder must not use width:100% of a tiny trigger */
        min-width: 0;
        width: 100%;
        max-width: 100%;
      }
      /* From ColorPicker/css/additional-css.css — inner-holder uses absolute for property editor panel */
      .s-color-picker {
        position: relative;
        display: block;
        width: 100%;
        max-width: 280px;
      }
      .s-color-picker .inner-holder {
        position: absolute;
        top: 100%;
        left: 0;
        margin-top: 4px;
        /* Fixed width: parent .s-color-picker was only as wide as the preview chip, so 100% collapsed */
        width: 252px;
        min-width: 252px;
        max-width: min(252px, calc(100vw - 48px));
        box-sizing: border-box;
        transition: 0.3s opacity;
        z-index: 10000;
        box-shadow: 0 5px 5px #00000017;
        background: white;
        border-radius: 4px;
        overflow: hidden;
      }
      .s-color-picker .inner-holder.hidden {
        pointer-events: none;
        visibility: hidden;
        opacity: 0;
      }
      .s-color-picker .inner-holder.visible {
        pointer-events: all;
        visibility: visible;
        opacity: 1;
      }
      .s-color-picker .a-color-picker {
        box-shadow: none;
        min-width: 0;
        width: 100%;
        box-sizing: border-box;
      }
      .s-color-picker [data-color-picker] {
        display: block;
        width: 100%;
        min-height: 1px;
      }
      .s-color-picker .additional-panel {
        padding: 15px;
        border-top: solid 1px #eeeeee;
      }
      .s-color-picker .additional-panel-title {
        font-family: Helvetica, Arial, sans-serif;
        font-size: 12px;
      }
      .s-color-picker .panel-colors-wrapper {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        padding-top: 3px;
        overflow-y: auto;
        overflow-x: hidden;
        max-height: 83px;
      }
      .s-color-picker .color-block {
        position: relative;
        width: 34px;
        height: 34px;
        border-radius: 4px;
        border: solid 1px #e0e0e0;
        box-sizing: border-box;
      }
      .s-color-picker .color-block.add-new {
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 0;
        background: #a6a6a6;
        cursor: pointer;
        border: none;
      }
      .s-color-picker .btn-color-delete {
        position: absolute;
        top: 0;
        right: 0;
        transform: translate(50%, -50%);
        width: 16px;
        height: 16px;
        display: flex;
        padding: 0;
        background-color: white;
        background-image: url("data:image/svg+xml,%3Csvg width='14' height='14' viewBox='0 0 14 14' fill='none' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath fill-rule='evenodd' clip-rule='evenodd' d='M1.71929 0.280712C1.53069 0.0985538 1.27808 -0.00224062 1.01589 3.78026e-05C0.75369 0.00231622 0.502878 0.107485 0.31747 0.292893C0.132062 0.478301 0.0268926 0.729114 0.0246142 0.991311C0.0223357 1.25351 0.12313 1.50611 0.305288 1.69471L5.59829 6.98771L0.305288 12.2807C0.209778 12.373 0.133596 12.4833 0.0811868 12.6053C0.0287778 12.7273 0.00119157 12.8585 3.77567e-05 12.9913C-0.00111606 13.1241 0.0241857 13.2558 0.0744665 13.3787C0.124747 13.5016 0.199001 13.6132 0.292893 13.7071C0.386786 13.801 0.498438 13.8753 0.621334 13.9255C0.744231 13.9758 0.87591 14.0011 1.00869 14C1.14147 13.9988 1.27269 13.9712 1.39469 13.9188C1.5167 13.8664 1.62704 13.7902 1.71929 13.6947L7.01229 8.40171L12.3053 13.6947C12.4939 13.8769 12.7465 13.9777 13.0087 13.9754C13.2709 13.9731 13.5217 13.8679 13.7071 13.6825C13.8925 13.4971 13.9977 13.2463 14 12.9841C14.0022 12.7219 13.9014 12.4693 13.7193 12.2807L8.42629 6.98771L13.7193 1.69471C13.9014 1.50611 14.0022 1.25351 14 0.991311C13.9977 0.729114 13.8925 0.478301 13.7071 0.292893C13.5217 0.107485 13.2709 0.00231622 13.0087 3.78026e-05C12.7465 -0.00224062 12.4939 0.0985538 12.3053 0.280712L7.01229 5.57371L1.71929 0.280712Z' fill='black'/%3E%3C/svg%3E%0A");
        background-size: 8px;
        background-position: center;
        background-repeat: no-repeat;
        border: none;
        cursor: pointer;
        opacity: 0;
        transition: 0.3s;
        pointer-events: none;
        border-radius: 50%;
      }
      .color-block:not(.add-new):hover .btn-color-delete {
        pointer-events: all;
        opacity: 1;
      }
      .color-block:not(.add-new).selected {
        border: solid 1px #68e25d;
      }
      .s-color-picker .picker-preview {
        display: flex;
        align-items: center;
      }
      .s-color-picker .picker-preview-inner {
        display: flex;
        align-items: center;
        padding: 5px;
        border-radius: 4px;
        border: solid 1px #eeeeee;
        cursor: pointer;
      }
      .s-color-picker .picker-preview .color-block {
        width: 24px;
        height: 24px;
        flex-shrink: 0;
        border-radius: 4px;
      }
      .s-color-picker .picker-preview-inner > svg {
        margin-left: 5px;
      }
      .s-color-picker .picker-preview .picker-preview-title {
        margin-bottom: 0;
        padding-left: 8px;
        font-family: var(--uui-font-family, "Lato", "Helvetica Neue", Helvetica, Arial, sans-serif);
        font-weight: 500;
        font-size: 13px;
        line-height: 1.25;
      }
      .s-color-picker .a-color-picker-single-input label {
        margin-bottom: 0;
        font-family: var(--uui-font-family, "Lato", "Helvetica Neue", Helvetica, Arial, sans-serif);
        font-size: 10px;
        line-height: 0;
      }
      .s-color-picker .a-color-picker-single-input input {
        font-family: var(--uui-font-family, "Lato", "Helvetica Neue", Helvetica, Arial, sans-serif);
        font-size: 13px;
      }
      .s-color-picker .btns-holder {
        display: flex;
        align-items: center;
        justify-content: flex-end;
        gap: 8px;
        padding: 0 15px 15px 15px;
      }
      .s-color-picker .btns-holder .picker-btn-cancel,
      .s-color-picker .btns-holder .picker-btn-choose {
        padding: 3px 10px;
        font-family: var(--uui-font-family, "Lato", "Helvetica Neue", Helvetica, Arial, sans-serif);
        font-weight: 500;
        font-size: 12px;
        border: none;
        border-radius: 4px;
        cursor: pointer;
      }
      .s-color-picker .btns-holder .picker-btn-choose {
        color: white;
        background-color: #3cab3b;
      }
      .s-color-picker .btns-holder .picker-btn-choose:hover {
        opacity: 0.8;
      }
      .s-color-picker .a-color-picker-circle {
        border-color: #e0e0e0;
      }
      .acp-error {
        font-size: 12px;
        color: var(--uui-color-danger, #d42054);
        margin-top: 8px;
      }
    `,
  ];

  render() {
    const display = this.value && String(this.value).trim() !== "" ? this.value : this.#getDefaultColor();
    const holderClass = this._visible ? "inner-holder visible" : "inner-holder hidden";

    return html`
      <div class="s-color-picker" data-s-color-picker>
        <div
          class="picker-preview"
          @click=${(/** @type {MouseEvent} */ e) => {
            e.stopPropagation();
            this.#toggle();
          }}
        >
          <div class="picker-preview-inner">
            <div class="color-block" style=${`background: ${display}`}></div>
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="currentColor" viewBox="0 0 16 16">
              <path
                fill-rule="evenodd"
                d="M1.646 4.646a.5.5 0 0 1 .708 0L8 10.293l5.646-5.647a.5.5 0 0 1 .708.708l-6 6a.5.5 0 0 1-.708 0l-6-6a.5.5 0 0 1 0-.708"
              />
            </svg>
          </div>
        </div>
        <div class=${holderClass}>
          <div data-color-picker></div>
          <div class="additional-panel">
            <p class="additional-panel-title"><b>Saved colors</b></p>
            <div class="panel-colors-wrapper">
              <button type="button" class="color-block add-new" @click=${this.#addColor}>
                <svg width="13" height="13" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                  <path
                    d="M4.97297 1.23077V2.78729C4.97297 3.16472 5.2973 3.47488 5.70292 3.47488H10.2971C10.3926 3.47531 10.4873 3.45785 10.5758 3.42351C10.6642 3.38917 10.7446 3.33862 10.8124 3.27475C10.8802 3.21088 10.9341 3.13495 10.9709 3.0513C11.0077 2.96765 11.0268 2.87794 11.027 2.78729V1.28575C10.8655 1.24953 10.7001 1.23108 10.5341 1.23077H4.97297ZM12.595 16H3.40584C2.95912 16.0009 2.51661 15.9182 2.1036 15.7567C1.6906 15.5951 1.3152 15.358 0.998883 15.0587C0.682565 14.7595 0.43153 14.404 0.260133 14.0126C0.0887361 13.6212 0.000340079 13.2016 0 12.7778V3.22134C0.000453674 2.7976 0.0889335 2.3781 0.260381 1.98682C0.431829 1.59554 0.682882 1.24016 0.999189 0.940991C1.3155 0.641819 1.69085 0.404726 2.1038 0.243266C2.51675 0.0818056 2.95919 -0.000856107 3.40584 6.685e-06H10.5341C11.4362 0.000684378 12.3015 0.339891 12.941 0.943596L15.0019 2.89149C15.3181 3.19017 15.569 3.54512 15.7403 3.93598C15.9115 4.32685 15.9998 4.74594 16 5.16923V12.777C15.9997 13.2008 15.9113 13.6203 15.7399 14.0116C15.5686 14.4029 15.3176 14.7584 15.0014 15.0576C14.6852 15.3569 14.3099 15.594 13.897 15.7556C13.4841 15.9172 13.0417 16.0007 12.595 16ZM11.027 14.7684V11.474C11.0268 11.3834 11.0077 11.2937 10.9709 11.21C10.9341 11.1264 10.8802 11.0505 10.8124 10.9866C10.7446 10.9227 10.6642 10.8722 10.5758 10.8378C10.4873 10.8035 10.3926 10.786 10.2971 10.7865H5.70292C5.60737 10.786 5.51267 10.8035 5.42424 10.8378C5.33581 10.8722 5.25539 10.9227 5.18759 10.9866C5.11979 11.0505 5.06593 11.1264 5.0291 11.21C4.99227 11.2937 4.9732 11.3834 4.97297 11.474V14.7684H11.027Z"
                    fill="white"
                  />
                </svg>
              </button>
              ${this._palette.map(
                (color) => html`
                  <div
                    class="color-block ${this.value === color ? "selected" : ""}"
                    style=${`background: ${color}`}
                    data-color-hex=${color}
                    @click=${this.#selectColor}
                  >
                    <button type="button" class="btn-color-delete" @click=${this.#deleteColor}></button>
                  </div>
                `
              )}
            </div>
          </div>
          <div class="btns-holder">
            <button
              type="button"
              class="picker-btn-cancel"
              @click=${(/** @type {MouseEvent} */ e) => {
                e.stopPropagation();
                this.#toggle("cancel");
              }}
            >
              Cancel
            </button>
            <button
              type="button"
              class="picker-btn-choose"
              @click=${(/** @type {MouseEvent} */ e) => {
                e.stopPropagation();
                this.#toggle("choose");
              }}
            >
              Choose
            </button>
          </div>
        </div>
      </div>
      ${!this._libReady && this._visible
        ? html`<p class="acp-error">Loading color library…</p>`
        : typeof window !== "undefined" && !window.AColorPicker && this._libReady
          ? html`<p class="acp-error">Could not load AColorPicker.</p>`
          : null}
    `;
  }
}

if (!customElements.get("dcms-color-picker-property-editor")) {
  customElements.define("dcms-color-picker-property-editor", DcmsColorPickerPropertyEditor);
}
