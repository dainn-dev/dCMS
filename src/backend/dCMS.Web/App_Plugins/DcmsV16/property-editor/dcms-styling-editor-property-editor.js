import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";

const DEFAULT_SCHEME = {
  content: {
    background: "#ffffff", h1: "#000000", h2: "#000000", h3: "#000000",
    h4: "#000000", h5: "#000000", h6: "#bcbcbc",
    primary: "#5b5b5b", secondary: "#5b5b5b", border: "#5b5b5b",
  },
  button: { text: "#ffffff", fill: "#2986cc", stroke: "#2986cc" },
  buttonSecondary: { text: "#2986cc", stroke: "#2986cc" },
};

const EDIT_SECTIONS = [
  {
    id: "content", label: "Content Colors",
    fields: [
      { key: "background", label: "Background" },
      { key: "h1", label: "H1" }, { key: "h2", label: "H2" },
      { key: "h3", label: "H3" }, { key: "h4", label: "H4" },
      { key: "h5", label: "H5" }, { key: "h6", label: "H6" },
      { key: "primary", label: "Primary" }, { key: "secondary", label: "Secondary" },
      { key: "border", label: "Border" },
    ],
  },
  {
    id: "button", label: "Primary Button",
    fields: [
      { key: "text", label: "Text" }, { key: "fill", label: "Fill" }, { key: "stroke", label: "Stroke" },
    ],
  },
  {
    id: "buttonSecondary", label: "Secondary Button",
    fields: [
      { key: "text", label: "Text" }, { key: "stroke", label: "Stroke" },
    ],
  },
];

function cloneScheme(s) {
  return JSON.parse(JSON.stringify(s));
}

function mergeScheme(s) {
  const d = cloneScheme(DEFAULT_SCHEME);
  if (!s || typeof s !== "object") return d;
  return {
    content: { ...d.content, ...(s.content ?? {}) },
    button: { ...d.button, ...(s.button ?? {}) },
    buttonSecondary: { ...d.buttonSecondary, ...(s.buttonSecondary ?? {}) },
  };
}

function parseValue(raw) {
  try {
    const obj = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (Array.isArray(obj)) return obj.map(mergeScheme);
  } catch { /* fall through */ }
  return [];
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

export default class DcmsStylingEditorPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _items: { state: true },   // scheme[]
    _editIdx: { state: true }, // number | null
    _editOpen: { state: true }, // Set<sectionId>
    _active: { state: true },  // { section, key, label } | null
  };

  constructor() {
    super();
    this.value = undefined;
    this._items = [];
    this._editIdx = null;
    this._editOpen = new Set(["content"]);
    this._active = null;
    /** @type {any} */
    this._picker = null;
    this._pickerMounted = false;
    this._onDocClick = this.#onDocClick.bind(this);
  }

  connectedCallback() {
    super.connectedCallback();
    this._items = parseValue(this.value);
    ensureAColorPicker().catch(() => {});
    document.addEventListener("click", this._onDocClick, true);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener("click", this._onDocClick, true);
    this.#teardown();
  }

  willUpdate(/** @type {Map<string,unknown>} */ changed) {
    if (changed.has("value")) this._items = parseValue(this.value);
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

    if (!this.shadowRoot?.querySelector("link#dcms-acp-se")) {
      const link = document.createElement("link");
      link.id = "dcms-acp-se"; link.rel = "stylesheet"; link.href = ACP_CSS;
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
      if (!this._active || this._editIdx === null) return;
      this.#applyColor(this._active.section, this._active.key, norm6(p?.all?.hexcss4));
    });
    this._pickerMounted = true;
  }

  #onDocClick(/** @type {MouseEvent} */ e) {
    if (!this._active) return;
    if (e.composedPath().includes(this)) return;
    this._active = null;
    this.#teardown();
  }

  // ── Item management ───────────────────────────────────────────────────────

  #addScheme() {
    this._items = [...this._items, cloneScheme(DEFAULT_SCHEME)];
    this._editIdx = this._items.length - 1;
    this._editOpen = new Set(["content"]);
    this._active = null;
    this.#teardown();
    this.#commit();
  }

  #copyScheme(/** @type {number} */ idx) {
    this._items = [
      ...this._items.slice(0, idx + 1),
      cloneScheme(this._items[idx]),
      ...this._items.slice(idx + 1),
    ];
    let editIdx = this._editIdx;
    if (editIdx !== null && editIdx > idx) editIdx++;
    this._editIdx = editIdx;
    this.#commit();
  }

  #deleteScheme(/** @type {number} */ idx) {
    this._items = this._items.filter((_, i) => i !== idx);
    let editIdx = this._editIdx;
    if (editIdx === idx) { editIdx = null; this._active = null; this.#teardown(); }
    else if (editIdx !== null && editIdx > idx) editIdx--;
    this._editIdx = editIdx;
    this.#commit();
  }

  #toggleEdit(/** @type {number} */ idx) {
    if (this._editIdx === idx) {
      this._editIdx = null;
      this._active = null;
      this.#teardown();
    } else {
      this._editIdx = idx;
      this._editOpen = new Set(["content"]);
      this._active = null;
      this.#teardown();
    }
  }

  #toggleEditSection(/** @type {string} */ id) {
    const s = new Set(this._editOpen);
    s.has(id) ? s.delete(id) : s.add(id);
    this._editOpen = s;
  }

  #applyColor(/** @type {string} */ section, /** @type {string} */ key, /** @type {string} */ hex) {
    if (this._editIdx === null) return;
    this._items = this._items.map((item, i) =>
      i !== this._editIdx ? item : { ...item, [section]: { ...item[section], [key]: hex } }
    );
    this.#commit();
  }

  #activateField(/** @type {string} */ section, /** @type {string} */ key, /** @type {string} */ label, /** @type {MouseEvent} */ e) {
    e.stopPropagation();
    if (this._active?.section === section && this._active?.key === key) {
      this._active = null;
      this.#teardown();
      return;
    }
    this._active = { section, key, label };
    const item = this._items[this._editIdx];
    this.#mountOrUpdate(norm6(item?.[section]?.[key] ?? "#000000"));
  }

  #onHexInput(/** @type {InputEvent} */ e) {
    if (!this._active || this._editIdx === null) return;
    const raw = /** @type {HTMLInputElement} */ (e.target).value;
    if (!/^#[0-9a-fA-F]{6}$/.test(raw)) return;
    const hex = raw.toLowerCase();
    this.#applyColor(this._active.section, this._active.key, hex);
    this._picker?.[0]?.setColor(hex);
  }

  #commit() {
    const next = JSON.stringify(this._items);
    if (next !== this.value) { this.value = next; this.dispatchEvent(new UmbChangeEvent()); }
  }

  // ── Styles ───────────────────────────────────────────────────────────────

  static styles = [
    UmbTextStyles,
    css`
      :host { display: block; }
      .s-se { max-width: 620px; }

      .scheme-grid { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 10px; }

      .scheme-card {
        position: relative; width: 153px;
        border: 2px solid var(--uui-color-border, #d8d7d9);
        border-radius: 6px; overflow: visible;
      }
      .scheme-card:hover { border-color: var(--uui-color-interactive, #1b264f); }
      .card-inner {
        padding: 10px 10px 8px;
        font-size: 11px; line-height: 1.5; border-radius: 4px;
      }
      .card-headings { margin-bottom: 3px; }
      .card-heading { font-weight: 700; }
      .card-hr { margin: 4px 0; border: none; border-top: 1px solid; }
      .card-btns { margin-top: 5px; display: flex; flex-direction: column; gap: 3px; }
      .card-btn-ex {
        padding: 2px 6px; font-size: 10px; border-radius: 3px;
        border: 1px solid; display: inline-block; line-height: 1.4;
      }
      .card-actions {
        position: absolute; top: -10px; right: -4px;
        display: flex; gap: 3px; opacity: 0; transition: opacity .15s;
        z-index: 10;
      }
      .scheme-card:hover .card-actions { opacity: 1; }
      .card-action-btn {
        width: 22px; height: 22px; border-radius: 3px;
        border: 1px solid rgba(0,0,0,.2); background: #fff;
        display: flex; align-items: center; justify-content: center;
        cursor: pointer; font-size: 11px; padding: 0; line-height: 1;
        box-shadow: 0 1px 3px rgba(0,0,0,.1);
      }
      .card-action-btn:hover { border-color: #333; }
      .card-action-btn.is-active { background: var(--uui-color-interactive, #1b264f); color: #fff; border-color: transparent; }

      .add-btn {
        display: inline-flex; align-items: center; gap: 6px;
        padding: 6px 14px; border-radius: 4px;
        border: 1px dashed var(--uui-color-border, #d8d7d9);
        background: var(--uui-color-surface-alt, #f6f5f7);
        cursor: pointer; font-size: 12px;
        color: var(--uui-color-contrast, #1b264f);
      }
      .add-btn:hover { border-color: var(--uui-color-interactive, #1b264f); }

      .edit-panel {
        margin-top: 10px;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px; overflow: hidden;
      }
      .edit-panel-header {
        padding: 6px 12px;
        background: var(--uui-color-surface-alt, #f6f5f7);
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
        font-size: 12px; font-weight: 600;
        color: var(--uui-color-contrast, #1b264f);
      }
      .edit-panel-body { padding: 10px; }

      .acc-item { border: 1px solid var(--uui-color-border,#d8d7d9); border-radius:4px; margin-bottom:6px; }
      .acc-header {
        display:flex; align-items:center; gap:10px; padding:6px 10px;
        cursor:pointer; user-select:none;
        background:var(--uui-color-surface-alt,#f6f5f7); border-radius:4px;
      }
      .acc-item.is-open .acc-header { border-radius:4px 4px 0 0; border-bottom:1px solid var(--uui-color-border,#d8d7d9); }
      .acc-chevron { font-size:10px; color:var(--uui-color-contrast-subdued,#9ca3af); transition:transform .2s; }
      .acc-item.is-open .acc-chevron { transform:rotate(90deg); }
      .acc-label { font-size:12px; font-weight:600; color:var(--uui-color-contrast,#1b264f); }
      .acc-body { display:none; padding:10px; }
      .acc-item.is-open .acc-body { display:block; }

      .color-grid { display:flex; flex-wrap:wrap; gap:8px 14px; }
      .color-field { display:flex; flex-direction:column; align-items:center; gap:4px; cursor:pointer; }
      .field-swatch {
        width:30px; height:30px; border-radius:4px;
        border:2px solid transparent; box-shadow:0 0 0 1px rgba(0,0,0,.15);
        transition:border-color .15s, box-shadow .15s;
      }
      .color-field:hover .field-swatch { box-shadow:0 0 0 1px var(--uui-color-interactive,#1b264f); }
      .color-field.is-active .field-swatch {
        border-color:var(--uui-color-interactive,#1b264f);
        box-shadow:0 0 0 3px rgba(41,134,204,.25);
      }
      .field-label { font-size:10px; color:var(--uui-color-contrast-subdued,#6b7280); text-align:center; max-width:52px; line-height:1.2; }
      .color-field.is-active .field-label { color:var(--uui-color-interactive,#1b264f); font-weight:600; }

      .picker-panel {
        margin-top:8px; border:1px solid var(--uui-color-border,#d8d7d9);
        border-radius:4px; background:var(--uui-color-surface,#fff); overflow:hidden;
      }
      .picker-bar {
        display:flex; align-items:center; gap:8px; padding:6px 10px;
        border-bottom:1px solid var(--uui-color-border,#d8d7d9);
        background:var(--uui-color-surface-alt,#f6f5f7);
      }
      .picker-swatch { width:16px; height:16px; border-radius:2px; border:1px solid rgba(0,0,0,.12); flex-shrink:0; }
      .picker-name { flex:1; font-size:11px; font-weight:600; color:var(--uui-color-contrast,#1b264f); }
      .picker-hex {
        width:74px; border:1px solid var(--uui-color-border,#d8d7d9); border-radius:2px;
        padding:2px 6px; font-size:11px; font-family:monospace;
        background:var(--uui-color-surface,#fff);
      }
      .picker-hex:focus { outline:none; border-color:var(--uui-color-interactive,#1b264f); }
      [data-acp] { display:block; min-height:1px; }
    `,
  ];

  // ── Render ───────────────────────────────────────────────────────────────

  #renderCard(/** @type {any} */ item, /** @type {number} */ idx) {
    const isEditing = this._editIdx === idx;
    return html`
      <div class="scheme-card" title="Style ${idx + 1}">
        <div class="card-inner" style=${`background:${item.content.background}`}>
          <div class="card-headings">
            ${["h1","h2","h3","h4","h5","h6"].map(h => html`<span class="card-heading" style=${`color:${item.content[h]}`}>${h} </span>`)}
          </div>
          <div style=${`color:${item.content.primary}; font-size:10px`}>text primary</div>
          <div style=${`color:${item.content.secondary}; font-size:10px`}>text secondary</div>
          <hr class="card-hr" style=${`border-color:${item.content.border}`} />
          <div class="card-btns">
            <div class="card-btn-ex" style=${`color:${item.button.text};background:${item.button.fill};border-color:${item.button.stroke}`}>Button primary</div>
            <div class="card-btn-ex" style=${`color:${item.buttonSecondary.text};border-color:${item.buttonSecondary.stroke}`}>Button secondary</div>
          </div>
        </div>
        <div class="card-actions">
          <button class="card-action-btn" title="Copy" @click=${() => this.#copyScheme(idx)}>⧉</button>
          <button class="card-action-btn ${isEditing ? "is-active" : ""}" title="Edit" @click=${() => this.#toggleEdit(idx)}>✎</button>
          <button class="card-action-btn" title="Delete" @click=${() => this.#deleteScheme(idx)}>✕</button>
        </div>
      </div>
    `;
  }

  #renderEditPanel() {
    if (this._editIdx === null) return null;
    const item = this._items[this._editIdx];
    if (!item) return null;
    const activeDisplay = this._active
      ? norm6(item[this._active.section]?.[this._active.key] ?? "#000000")
      : null;

    return html`
      <div class="edit-panel">
        <div class="edit-panel-header">Editing Style ${this._editIdx + 1}</div>
        <div class="edit-panel-body">
          ${EDIT_SECTIONS.map((sec) => {
            const isOpen = this._editOpen.has(sec.id);
            return html`
              <div class="acc-item ${isOpen ? "is-open" : ""}">
                <div class="acc-header" @click=${() => this.#toggleEditSection(sec.id)}>
                  <span class="acc-chevron">▶</span>
                  <span class="acc-label">${sec.label}</span>
                </div>
                <div class="acc-body">
                  <div class="color-grid">
                    ${sec.fields.map((f) => {
                      const display = norm6(item[sec.id]?.[f.key] ?? "#000000");
                      const isActive = this._active?.section === sec.id && this._active?.key === f.key;
                      return html`
                        <div
                          class="color-field ${isActive ? "is-active" : ""}"
                          @click=${(/** @type {MouseEvent} */ e) => this.#activateField(sec.id, f.key, f.label, e)}
                          title=${f.label}
                        >
                          <div class="field-swatch" style=${`background:${display}`}></div>
                          <span class="field-label">${f.label}</span>
                        </div>
                      `;
                    })}
                  </div>
                </div>
              </div>
            `;
          })}

          ${this._active ? html`
            <div class="picker-panel">
              <div class="picker-bar">
                <div class="picker-swatch" style=${`background:${activeDisplay}`}></div>
                <span class="picker-name">${this._active.label}</span>
                <input
                  class="picker-hex"
                  type="text"
                  maxlength="7"
                  .value=${activeDisplay ?? ""}
                  @input=${this.#onHexInput}
                />
              </div>
              <div data-acp></div>
            </div>
          ` : null}
        </div>
      </div>
    `;
  }

  render() {
    return html`
      <div class="s-se">
        <div class="scheme-grid">
          ${this._items.map((item, idx) => this.#renderCard(item, idx))}
        </div>
        <button type="button" class="add-btn" @click=${this.#addScheme}>
          <span>+</span> Add color style
        </button>
        ${this.#renderEditPanel()}
      </div>
    `;
  }
}

if (!customElements.get("dcms-styling-editor-property-editor")) {
  customElements.define("dcms-styling-editor-property-editor", DcmsStylingEditorPropertyEditor);
}
