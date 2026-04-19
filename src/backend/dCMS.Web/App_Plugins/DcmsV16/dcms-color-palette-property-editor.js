import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";

const DEFAULT_COLORS = ["000000", "5b5b5b", "bcbcbc", "f3f6f4", "ffffff", "2986cc", "174a71"];

/** Strip # and alpha, return lowercase 6-char hex */
function toHex6(raw) {
  return String(raw ?? "").replace(/^#/, "").slice(0, 6).toLowerCase() || "000000";
}

/** Parse stored JSON value → sorted item array */
function parseValue(raw) {
  try {
    const obj = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (obj && typeof obj === "object" && !Array.isArray(obj)) {
      const items = Object.entries(obj).map(([key, v]) => ({
        key,
        value: toHex6(v.value),
        label: String(v.label ?? v.value ?? ""),
        sortOrder: Number(v.sortOrder) || 0,
      }));
      if (items.length) return items.sort((a, b) => a.sortOrder - b.sortOrder);
    }
  } catch {
    /* fall through */
  }
  return DEFAULT_COLORS.map((c, i) => ({ key: String(i + 1), value: c, label: c, sortOrder: i + 1 }));
}

/** Serialize item array → JSON string (value stored by Umbraco) */
function serializeItems(items) {
  const obj = {};
  items.forEach((item, i) => {
    obj[item.key] = { value: item.value, label: item.label || item.value, sortOrder: i + 1 };
  });
  return JSON.stringify(obj);
}

/** @type {Promise<void> | null} */
let acpLoadPromise = null;

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

export default class DcmsColorPalettePropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    /** @type {Array<{key:string, value:string, label:string, sortOrder:number}>} */
    _items: { state: true },
    /** current hex6 in picker (no #) */
    _newColor: { state: true },
    /** key of item being edited, null = adding new */
    _editKey: { state: true },
    _pickerOpen: { state: true },
    _pickerMounted: { state: true },
    _libReady: { state: true },
    /** key being dragged */
    _dragKey: { state: true },
    _dragOverKey: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this._items = parseValue(undefined);
    this._newColor = "2986cc";
    this._editKey = null;
    this._pickerOpen = false;
    this._pickerMounted = false;
    this._libReady = false;
    this._dragKey = null;
    this._dragOverKey = null;
    /** @type {any} */
    this._picker = null;
    this._onDocClick = this.#onDocClick.bind(this);
  }

  connectedCallback() {
    super.connectedCallback();
    this._items = parseValue(this.value);
    ensureAColorPicker()
      .then(() => { this._libReady = true; })
      .catch(() => { this._libReady = false; });
    document.addEventListener("click", this._onDocClick, true);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener("click", this._onDocClick, true);
    this.#teardownPicker();
  }

  willUpdate(/** @type {Map<string,unknown>} */ changed) {
    if (changed.has("value")) {
      this._items = parseValue(this.value);
    }
  }

  #onDocClick(/** @type {MouseEvent} */ e) {
    if (!this._pickerOpen) return;
    if (e.composedPath().includes(this)) return;
    this._pickerOpen = false;
    this.#teardownPicker();
  }

  #teardownPicker() {
    this._pickerMounted = false;
    this._picker = null;
    const host = this.renderRoot?.querySelector("[data-acp-host]");
    if (host) host.innerHTML = "";
  }

  async #openPicker() {
    this._pickerOpen = true;
    await this.updateComplete;
    if (this._pickerMounted) {
      this._picker?.[0]?.setColor(`#${this._newColor}`);
      return;
    }
    await ensureAColorPicker();
    if (!window.AColorPicker) return;

    // inject CSS once
    if (!this.shadowRoot?.querySelector("link#dcms-acp-pal")) {
      const link = document.createElement("link");
      link.id = "dcms-acp-pal";
      link.rel = "stylesheet";
      link.href = ACP_CSS;
      this.shadowRoot?.appendChild(link);
    }

    const host = this.renderRoot.querySelector("[data-acp-host]");
    if (!host) return;
    host.setAttribute("acp-color", `#${this._newColor}`);
    host.setAttribute("acp-show-alpha", "no");
    host.setAttribute("acp-show-rgb", "no");
    host.setAttribute("acp-show-hsl", "no");

    this._picker = window.AColorPicker.from(host, { hueBarSize: [220, 130], slBarSize: [220, 130] });
    this._picker.on("change", (/** @type {any} */ p) => {
      this._newColor = toHex6(p?.all?.hexcss4 ?? this._newColor);
    });
    this._pickerMounted = true;
  }

  #closePicker() {
    this._pickerOpen = false;
    this.#teardownPicker();
  }

  #togglePicker(/** @type {MouseEvent} */ e) {
    e.stopPropagation();
    if (this._pickerOpen) {
      this.#closePicker();
    } else {
      this.#openPicker();
    }
  }

  #onHexInput(/** @type {InputEvent} */ e) {
    const raw = /** @type {HTMLInputElement} */ (e.target).value.replace(/^#/, "");
    if (/^[0-9a-fA-F]{6}$/.test(raw)) {
      this._newColor = raw.toLowerCase();
      this._picker?.[0]?.setColor(`#${this._newColor}`);
    }
  }

  #addOrUpdate() {
    if (this._editKey) {
      this._items = this._items.map((item) =>
        item.key === this._editKey ? { ...item, value: this._newColor, label: this._newColor } : item
      );
      this._editKey = null;
    } else {
      if (this._items.length >= 64) return;
      const nextKey = String(Date.now());
      this._items = [...this._items, { key: nextKey, value: this._newColor, label: this._newColor, sortOrder: this._items.length + 1 }];
    }
    this.#closePicker();
    this.#commit();
  }

  #startEdit(/** @type {any} */ item) {
    this._editKey = item.key;
    this._newColor = item.value;
    this.#openPicker();
  }

  #cancelEdit() {
    this._editKey = null;
    this._newColor = "2986cc";
    this.#closePicker();
  }

  #remove(/** @type {string} */ key) {
    this._items = this._items.filter((i) => i.key !== key);
    this.#commit();
  }

  #commit() {
    const next = serializeItems(this._items);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  // ── Drag & Drop ──────────────────────────────────────────────────────────

  #onDragStart(/** @type {DragEvent} */ e, /** @type {string} */ key) {
    this._dragKey = key;
    if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
  }

  #onDragOver(/** @type {DragEvent} */ e, /** @type {string} */ key) {
    e.preventDefault();
    if (e.dataTransfer) e.dataTransfer.dropEffect = "move";
    this._dragOverKey = key;
  }

  #onDragLeave() {
    this._dragOverKey = null;
  }

  #onDrop(/** @type {DragEvent} */ e, /** @type {string} */ targetKey) {
    e.preventDefault();
    const fromKey = this._dragKey;
    this._dragKey = null;
    this._dragOverKey = null;
    if (!fromKey || fromKey === targetKey) return;
    const items = [...this._items];
    const fromIdx = items.findIndex((i) => i.key === fromKey);
    const toIdx = items.findIndex((i) => i.key === targetKey);
    if (fromIdx < 0 || toIdx < 0) return;
    const [moved] = items.splice(fromIdx, 1);
    items.splice(toIdx, 0, moved);
    this._items = items.map((item, i) => ({ ...item, sortOrder: i + 1 }));
    this.#commit();
  }

  #onDragEnd() {
    this._dragKey = null;
    this._dragOverKey = null;
  }

  // ── Styles ───────────────────────────────────────────────────────────────

  static styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }
      .s-color-palette {
        max-width: 380px;
      }

      /* ── Add / Edit row ── */
      .add-row {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-wrap: wrap;
        position: relative;
        padding-bottom: 12px;
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
        margin-bottom: 12px;
      }
      .picker-trigger {
        display: flex;
        align-items: center;
        gap: 6px;
        padding: 4px 8px;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 3px;
        cursor: pointer;
        background: var(--uui-color-surface, #fff);
        font-size: 12px;
        font-family: monospace;
      }
      .picker-trigger:hover {
        border-color: var(--uui-color-interactive, #1b264f);
      }
      .trigger-swatch {
        width: 20px;
        height: 20px;
        border-radius: 2px;
        border: 1px solid rgba(0, 0, 0, 0.15);
        flex-shrink: 0;
      }
      .hex-input {
        width: 76px;
        box-sizing: border-box;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        padding: 4px 6px;
        font-size: 12px;
        font-family: monospace;
        background: var(--uui-color-surface, #fff);
        color: var(--uui-color-contrast, #1b264f);
      }
      .hex-input:focus {
        outline: none;
        border-color: var(--uui-color-interactive, #1b264f);
      }
      .picker-panel {
        position: absolute;
        top: calc(100% - 2px);
        left: 0;
        z-index: 9999;
        background: var(--uui-color-surface, #fff);
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px;
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
        overflow: hidden;
      }
      .picker-panel.hidden {
        display: none;
      }
      [data-acp-host] {
        display: block;
        min-height: 1px;
      }

      /* ── Action buttons ── */
      .btn-add,
      .btn-update,
      .btn-cancel {
        padding: 4px 10px;
        font-size: 12px;
        font-family: var(--uui-font-family, sans-serif);
        font-weight: 500;
        border: none;
        border-radius: 3px;
        cursor: pointer;
      }
      .btn-add,
      .btn-update {
        background: var(--uui-color-interactive, #1b264f);
        color: #fff;
      }
      .btn-add:hover,
      .btn-update:hover {
        opacity: 0.85;
      }
      .btn-cancel {
        background: transparent;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        color: var(--uui-color-contrast, #1b264f);
      }
      .btn-cancel:hover {
        background: var(--uui-color-surface-alt, #f6f5f7);
      }

      /* ── Palette list ── */
      .palette-list {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
      .palette-item {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 6px 8px;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 3px;
        background: var(--uui-color-surface, #fff);
        transition: border-color 0.15s;
        cursor: default;
      }
      .palette-item.drag-over {
        border-color: var(--uui-color-interactive, #1b264f);
        background: var(--uui-color-surface-emphasis, #eef2ff);
      }
      .drag-handle {
        cursor: grab;
        color: var(--uui-color-contrast-subdued, #9ca3af);
        font-size: 14px;
        user-select: none;
        padding: 0 2px;
        flex-shrink: 0;
      }
      .drag-handle:active {
        cursor: grabbing;
      }
      .item-swatch {
        width: 28px;
        height: 28px;
        border-radius: 3px;
        border: 1px solid rgba(0, 0, 0, 0.12);
        flex-shrink: 0;
      }
      .item-hex {
        flex: 1;
        font-family: monospace;
        font-size: 12px;
        color: var(--uui-color-contrast, #1b264f);
      }
      .btn-item-edit,
      .btn-item-remove {
        padding: 2px 7px;
        font-size: 11px;
        font-family: var(--uui-font-family, sans-serif);
        border-radius: 3px;
        cursor: pointer;
        flex-shrink: 0;
      }
      .btn-item-edit {
        background: transparent;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        color: var(--uui-color-contrast, #1b264f);
      }
      .btn-item-edit:hover {
        background: var(--uui-color-surface-alt, #f6f5f7);
      }
      .btn-item-remove {
        background: transparent;
        border: 1px solid transparent;
        color: var(--uui-color-danger, #d42054);
        font-size: 14px;
        line-height: 1;
        padding: 2px 5px;
      }
      .btn-item-remove:hover {
        background: var(--uui-color-danger-standalone, #fef2f4);
      }
      .empty-hint {
        font-size: 12px;
        color: var(--uui-color-contrast-subdued, #9ca3af);
        padding: 8px 0;
      }
    `,
  ];

  render() {
    const isEditing = this._editKey !== null;

    return html`
      <div class="s-color-palette">

        <!-- Add / Edit row -->
        <div class="add-row">
          <div
            class="picker-trigger"
            @click=${this.#togglePicker}
            title="Pick color"
          >
            <div class="trigger-swatch" style=${`background:#${this._newColor}`}></div>
            <span>#${this._newColor}</span>
          </div>

          <input
            class="hex-input"
            type="text"
            maxlength="7"
            .value=${"#" + this._newColor}
            @input=${this.#onHexInput}
            placeholder="#rrggbb"
          />

          ${isEditing
            ? html`
                <button class="btn-update" type="button" @click=${this.#addOrUpdate}>Update</button>
                <button class="btn-cancel" type="button" @click=${this.#cancelEdit}>Cancel</button>
              `
            : html`
                <button
                  class="btn-add"
                  type="button"
                  ?disabled=${this._items.length >= 64}
                  @click=${this.#addOrUpdate}
                >Add</button>
              `}

          <div class="picker-panel ${this._pickerOpen ? "" : "hidden"}">
            <div data-acp-host></div>
          </div>
        </div>

        <!-- Palette list -->
        <div class="palette-list">
          ${this._items.length === 0
            ? html`<p class="empty-hint">No colors yet. Use the picker above to add.</p>`
            : this._items.map(
                (item) => html`
                  <div
                    class="palette-item ${this._dragOverKey === item.key ? "drag-over" : ""}"
                    draggable="true"
                    @dragstart=${(/** @type {DragEvent} */ e) => this.#onDragStart(e, item.key)}
                    @dragover=${(/** @type {DragEvent} */ e) => this.#onDragOver(e, item.key)}
                    @dragleave=${this.#onDragLeave}
                    @drop=${(/** @type {DragEvent} */ e) => this.#onDrop(e, item.key)}
                    @dragend=${this.#onDragEnd}
                  >
                    <span class="drag-handle" title="Drag to reorder">⠿</span>
                    <div class="item-swatch" style=${`background:#${item.value}`}></div>
                    <span class="item-hex">#${item.value}</span>
                    <button
                      class="btn-item-edit"
                      type="button"
                      @click=${() => this.#startEdit(item)}
                    >Edit</button>
                    <button
                      class="btn-item-remove"
                      type="button"
                      title="Remove"
                      @click=${() => this.#remove(item.key)}
                    >✕</button>
                  </div>
                `
              )}
        </div>
      </div>
    `;
  }
}

if (!customElements.get("dcms-color-palette-property-editor")) {
  customElements.define("dcms-color-palette-property-editor", DcmsColorPalettePropertyEditor);
}
