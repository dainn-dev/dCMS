import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";

const DEFAULT_VALUE = {
  content: {
    background: "#ffffff", h1: "#000000", h2: "#000000", h3: "#000000",
    h4: "#000000", h5: "#000000", h6: "#bcbcbc",
    primary: "#5b5b5b", secondary: "#5b5b5b", border: "#5b5b5b",
    link: "#2986cc", linkHover: "#bcbcbc",
  },
  button: {
    text: "#ffffff", textHover: "#ffffff",
    fill: "#2986cc", fillHover: "#174a71",
    stroke: "#2986cc", strokeHover: "#174a71",
  },
  buttonSecondary: {
    text: "#2986cc", textHover: "#174a71",
    fill: "#ffffff", fillHover: "#ffffff",
    stroke: "#2986cc", strokeHover: "#174a71",
  },
};

const SECTIONS = [
  {
    id: "content", label: "Baseline content colors", previewKey: "background",
    fields: [
      { key: "background", label: "Block Background" },
      { key: "h1", label: "H1" }, { key: "h2", label: "H2" },
      { key: "h3", label: "H3" }, { key: "h4", label: "H4" },
      { key: "h5", label: "H5" }, { key: "h6", label: "H6" },
      { key: "primary", label: "Body primary" }, { key: "secondary", label: "Body secondary" },
      { key: "border", label: "Border" }, { key: "link", label: "Link" },
      { key: "linkHover", label: "Link Hover" },
    ],
  },
  {
    id: "button", label: "Primary Button", previewKey: "fill",
    fields: [
      { key: "text", label: "Text" }, { key: "textHover", label: "Text Hover" },
      { key: "fill", label: "Fill" }, { key: "fillHover", label: "Fill Hover" },
      { key: "stroke", label: "Stroke" }, { key: "strokeHover", label: "Stroke Hover" },
    ],
  },
  {
    id: "buttonSecondary", label: "Secondary Button", previewKey: "stroke",
    fields: [
      { key: "text", label: "Text" }, { key: "textHover", label: "Text Hover" },
      { key: "fill", label: "Fill" }, { key: "fillHover", label: "Fill Hover" },
      { key: "stroke", label: "Stroke" }, { key: "strokeHover", label: "Stroke Hover" },
    ],
  },
];

function mergeDefaults(target, defaults) {
  const out = { ...target };
  for (const k of Object.keys(defaults)) {
    if (out[k] == null) out[k] = defaults[k];
    else if (typeof defaults[k] === "object" && !Array.isArray(defaults[k]))
      out[k] = mergeDefaults(out[k] ?? {}, defaults[k]);
  }
  return out;
}

function parseValue(raw) {
  try {
    const obj = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (obj && typeof obj === "object") return mergeDefaults(obj, DEFAULT_VALUE);
  } catch { /* fall through */ }
  return JSON.parse(JSON.stringify(DEFAULT_VALUE));
}

function norm6(raw) {
  const s = String(raw ?? "").replace(/^#/, "").slice(0, 6).toLowerCase();
  return s.length === 6 ? "#" + s : "#000000";
}

/** @type {Promise<void>|null} */
let acpLoadPromise = null;
function ensureAColorPicker() {
  if (typeof window !== "undefined" && window.AColorPicker) return Promise.resolve();
  if (!acpLoadPromise) {
    acpLoadPromise = new Promise((resolve, reject) => {
      const ex = document.querySelector(`script[src="${ACP_JS}"]`);
      if (ex) { ex.addEventListener("load", resolve, { once: true }); ex.addEventListener("error", reject, { once: true }); return; }
      const s = document.createElement("script");
      s.src = ACP_JS; s.async = true; s.onload = resolve; s.onerror = reject;
      document.head.appendChild(s);
    });
  }
  return acpLoadPromise;
}

export default class DcmsBaselineColorsPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _cv: { state: true },
    _open: { state: true },   // Set<sectionId>
    _active: { state: true }, // { section, key, label } | null
  };

  constructor() {
    super();
    this.value = undefined;
    this._cv = parseValue(undefined);
    this._open = new Set(["content"]);
    this._active = null;
    /** @type {any} */
    this._picker = null;
    this._pickerMounted = false;
    this._onDocClick = this.#onDocClick.bind(this);
  }

  connectedCallback() {
    super.connectedCallback();
    this._cv = parseValue(this.value);
    ensureAColorPicker().catch(() => {});
    document.addEventListener("click", this._onDocClick, true);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener("click", this._onDocClick, true);
    this.#teardown();
  }

  willUpdate(/** @type {Map<string,unknown>} */ changed) {
    if (changed.has("value")) this._cv = parseValue(this.value);
  }

  // ── Picker ───────────────────────────────────────────────────────────────

  #teardown() {
    this._pickerMounted = false;
    this._picker = null;
    const host = this.renderRoot?.querySelector("[data-acp]");
    if (host) host.innerHTML = "";
  }

  async #mountOrUpdate(hex) {
    await this.updateComplete;
    await ensureAColorPicker();
    if (!window.AColorPicker) return;

    if (!this.shadowRoot?.querySelector("link#dcms-acp-bc")) {
      const link = document.createElement("link");
      link.id = "dcms-acp-bc"; link.rel = "stylesheet"; link.href = ACP_CSS;
      this.shadowRoot?.appendChild(link);
    }

    const host = this.renderRoot.querySelector("[data-acp]");
    if (!host) return;

    if (this._pickerMounted) {
      this._picker?.[0]?.setColor(hex);
      return;
    }

    host.setAttribute("acp-color", hex);
    host.setAttribute("acp-show-alpha", "no");
    host.setAttribute("acp-show-rgb", "no");
    host.setAttribute("acp-show-hsl", "no");

    this._picker = window.AColorPicker.from(host, { hueBarSize: [230, 140], slBarSize: [230, 140] });
    this._picker.on("change", (/** @type {any} */ p) => {
      if (!this._active) return;
      const h = norm6(p?.all?.hexcss4);
      this._cv = {
        ...this._cv,
        [this._active.section]: { ...this._cv[this._active.section], [this._active.key]: h },
      };
      this.#commit();
    });
    this._pickerMounted = true;
  }

  #onDocClick(/** @type {MouseEvent} */ e) {
    if (!this._active) return;
    if (e.composedPath().includes(this)) return;
    this._active = null;
    this.#teardown();
  }

  // ── Handlers ─────────────────────────────────────────────────────────────

  #toggleSection(/** @type {string} */ id) {
    const s = new Set(this._open);
    s.has(id) ? s.delete(id) : s.add(id);
    this._open = s;
  }

  #activateField(/** @type {string} */ section, /** @type {string} */ key, /** @type {string} */ label, /** @type {MouseEvent} */ e) {
    e.stopPropagation();
    if (this._active?.section === section && this._active?.key === key) {
      this._active = null;
      this.#teardown();
      return;
    }
    this._active = { section, key, label };
    const hex = norm6(this._cv[section]?.[key] ?? "#000000");
    this.#mountOrUpdate(hex);
  }

  #onHexInput(/** @type {InputEvent} */ e) {
    if (!this._active) return;
    const raw = /** @type {HTMLInputElement} */ (e.target).value;
    if (!/^#[0-9a-fA-F]{6}$/.test(raw)) return;
    const hex = raw.toLowerCase();
    this._cv = { ...this._cv, [this._active.section]: { ...this._cv[this._active.section], [this._active.key]: hex } };
    this._picker?.[0]?.setColor(hex);
    this.#commit();
  }

  #commit() {
    const next = JSON.stringify(this._cv);
    if (next !== this.value) { this.value = next; this.dispatchEvent(new UmbChangeEvent()); }
  }

  // ── Styles ───────────────────────────────────────────────────────────────

  static styles = [
    UmbTextStyles,
    css`
      :host { display: block; }
      .s-baseline { max-width: 500px; }

      .acc-item {
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px;
        margin-bottom: 6px;
      }
      .acc-header {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px 12px;
        cursor: pointer;
        user-select: none;
        background: var(--uui-color-surface-alt, #f6f5f7);
        border-radius: 4px;
      }
      .acc-item.is-open .acc-header {
        border-radius: 4px 4px 0 0;
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
      }
      .acc-chevron { font-size: 10px; color: var(--uui-color-contrast-subdued, #9ca3af); transition: transform 0.2s; }
      .acc-item.is-open .acc-chevron { transform: rotate(90deg); }
      .acc-preview { width: 18px; height: 18px; border-radius: 3px; border: 1px solid rgba(0,0,0,0.12); flex-shrink: 0; }
      .acc-label { flex: 1; font-size: 13px; font-weight: 600; color: var(--uui-color-contrast, #1b264f); }
      .acc-body { display: none; padding: 12px 12px 10px; }
      .acc-item.is-open .acc-body { display: block; }

      .color-grid { display: flex; flex-wrap: wrap; gap: 10px 16px; }
      .color-field { display: flex; flex-direction: column; align-items: center; gap: 4px; cursor: pointer; }
      .field-swatch {
        width: 34px; height: 34px;
        border-radius: 4px;
        border: 2px solid transparent;
        box-shadow: 0 0 0 1px rgba(0,0,0,0.15);
        transition: border-color 0.15s, box-shadow 0.15s;
      }
      .color-field:hover .field-swatch { box-shadow: 0 0 0 1px var(--uui-color-interactive, #1b264f); }
      .color-field.is-active .field-swatch {
        border-color: var(--uui-color-interactive, #1b264f);
        box-shadow: 0 0 0 3px rgba(41,134,204,0.25);
      }
      .field-label { font-size: 10px; color: var(--uui-color-contrast-subdued, #6b7280); text-align: center; max-width: 52px; line-height: 1.2; }
      .color-field.is-active .field-label { color: var(--uui-color-interactive, #1b264f); font-weight: 600; }

      /* Shared picker panel — rendered once below all sections */
      .picker-panel {
        margin-top: 8px;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px;
        background: var(--uui-color-surface, #fff);
        overflow: hidden;
      }
      .picker-bar {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 6px 10px;
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
        background: var(--uui-color-surface-alt, #f6f5f7);
      }
      .picker-swatch { width: 16px; height: 16px; border-radius: 2px; border: 1px solid rgba(0,0,0,0.12); flex-shrink: 0; }
      .picker-name { flex: 1; font-size: 11px; font-weight: 600; color: var(--uui-color-contrast, #1b264f); }
      .picker-hex {
        width: 74px;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        padding: 2px 6px;
        font-size: 11px;
        font-family: monospace;
        background: var(--uui-color-surface, #fff);
      }
      .picker-hex:focus { outline: none; border-color: var(--uui-color-interactive, #1b264f); }
      [data-acp] { display: block; min-height: 1px; }
    `,
  ];

  // ── Render ───────────────────────────────────────────────────────────────

  render() {
    const activeColor = this._active
      ? norm6(this._cv[this._active.section]?.[this._active.key] ?? "#000000")
      : null;

    return html`
      <div class="s-baseline">

        ${SECTIONS.map((sec) => {
          const isOpen = this._open.has(sec.id);
          const preview = norm6(this._cv[sec.id]?.[sec.previewKey] ?? "#cccccc");
          return html`
            <div class="acc-item ${isOpen ? "is-open" : ""}">
              <div class="acc-header" @click=${() => this.#toggleSection(sec.id)}>
                <span class="acc-chevron">▶</span>
                <div class="acc-preview" style=${`background:${preview}`}></div>
                <span class="acc-label">${sec.label}</span>
              </div>
              <div class="acc-body">
                <div class="color-grid">
                  ${sec.fields.map((f) => {
                    const color = norm6(this._cv[sec.id]?.[f.key] ?? "#000000");
                    const isActive = this._active?.section === sec.id && this._active?.key === f.key;
                    return html`
                      <div
                        class="color-field ${isActive ? "is-active" : ""}"
                        @click=${(/** @type {MouseEvent} */ e) => this.#activateField(sec.id, f.key, f.label, e)}
                        title=${f.label}
                      >
                        <div class="field-swatch" style=${`background:${color}`}></div>
                        <span class="field-label">${f.label}</span>
                      </div>
                    `;
                  })}
                </div>
              </div>
            </div>
          `;
        })}

        <!-- Single shared picker panel at the bottom -->
        ${this._active ? html`
          <div class="picker-panel">
            <div class="picker-bar">
              <div class="picker-swatch" style=${`background:${activeColor}`}></div>
              <span class="picker-name">${this._active.label}</span>
              <input
                class="picker-hex"
                type="text"
                maxlength="7"
                .value=${activeColor ?? ""}
                @input=${this.#onHexInput}
              />
            </div>
            <div data-acp></div>
          </div>
        ` : null}

      </div>
    `;
  }
}

if (!customElements.get("dcms-baseline-colors-property-editor")) {
  customElements.define("dcms-baseline-colors-property-editor", DcmsBaselineColorsPropertyEditor);
}
