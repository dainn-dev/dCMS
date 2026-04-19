import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const WEBFONTS_URL = "/App_Plugins/DcmsV16/font-picker/webfonts.json";
const GOOGLE_FONTS_BASE = "https://fonts.googleapis.com/css?family=";
export const FONTS_CHANNEL = "dcms_fonts_v1";
export const FONTS_LS_KEY = "dcms.fonts.v1";

function parseFontVariants(variants) {
  const map = {};
  for (const v of variants) {
    const m = v.match(/^(\d+)?([a-z]+)?$/);
    if (!m) continue;
    const weight = m[1] || "400";
    const style = !m[2] || m[2] === "regular" ? "normal" : m[2];
    if (!map[style]) map[style] = [];
    map[style].push(weight);
  }
  return map;
}

function getFontDetails(fonts, fontFamily) {
  const found = fonts.find((f) => f.family === fontFamily);
  if (!found) return { variants: { normal: ["400"] }, category: "sans-serif" };
  return { variants: parseFontVariants(found.variants), category: found.category };
}

function buildGoogleFontsUrl(fontFamily, variantsMap) {
  const parts = [];
  for (const [style, weights] of Object.entries(variantsMap)) {
    const suffix = style === "normal" ? "" : style[0];
    parts.push(...weights.map((w) => w + suffix));
  }
  return GOOGLE_FONTS_BASE + encodeURIComponent(fontFamily).replace(/%20/g, "+") + ":" + parts.join(",") + "&display=swap";
}

const _loadedFonts = new Set();
function loadGoogleFont(fonts, fontFamily) {
  if (_loadedFonts.has(fontFamily)) return;
  _loadedFonts.add(fontFamily);
  const details = getFontDetails(fonts, fontFamily);
  const url = buildGoogleFontsUrl(fontFamily, details.variants);
  if (!document.querySelector(`link[href="${url}"]`)) {
    const link = document.createElement("link");
    link.rel = "stylesheet"; link.href = url;
    document.head.appendChild(link);
  }
}

function makeItem(fontFamily = "Poppins", fontStyle = "normal", fontWeight = "400", fontCategory = "sans-serif") {
  return { fontFamily, fontCategory, fontStyle, fontWeight };
}

const DEFAULT_ITEMS = [
  makeItem("Poppins", "normal", "400", "sans-serif"),
  makeItem("DM Sans", "normal", "500", "sans-serif"),
  makeItem("DM Sans", "normal", "700", "sans-serif"),
];

function parseValue(raw) {
  try {
    const arr = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (Array.isArray(arr) && arr.length) return arr.map((x) => ({ ...makeItem(), ...x }));
  } catch { /* fall through */ }
  return DEFAULT_ITEMS.map((x) => ({ ...x }));
}

export default class DcmsFontsPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _items: { state: true },
    _open: { state: true },    // Set<number>
    _fonts: { state: true },   // WebfontItem[] | null
    _filters: { state: true }, // string[] — per-item family search
    _dragging: { state: true },
    _dragOver: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this._items = DEFAULT_ITEMS.map((x) => ({ ...x }));
    this._open = new Set([0]);
    this._fonts = null;
    this._filters = ["", "", ""];
    this._dragging = null;
    this._dragOver = null;
  }

  connectedCallback() {
    super.connectedCallback();
    this._items = parseValue(this.value);
    this._filters = this._items.map(() => "");
    this.#loadFonts();
  }

  willUpdate(/** @type {Map<string,unknown>} */ changed) {
    if (changed.has("value")) {
      this._items = parseValue(this.value);
      this._filters = this._items.map(() => "");
    }
  }

  async #loadFonts() {
    try {
      const res = await fetch(WEBFONTS_URL);
      const data = await res.json();
      this._fonts = data.items ?? [];
      for (const item of this._items) {
        loadGoogleFont(this._fonts, item.fontFamily);
      }
    } catch {
      this._fonts = [];
    }
  }

  // ── Handlers ──────────────────────────────────────────────────────────────

  #toggleOpen(idx) {
    const s = new Set(this._open);
    s.has(idx) ? s.delete(idx) : s.add(idx);
    this._open = s;
  }

  #addItem() {
    const item = makeItem();
    this._items = [...this._items, item];
    this._filters = [...this._filters, ""];
    const newIdx = this._items.length - 1;
    this._open = new Set([...this._open, newIdx]);
    if (this._fonts) loadGoogleFont(this._fonts, item.fontFamily);
    this.#commit();
  }

  #removeItem(idx) {
    if (this._items.length <= 1) return;
    this._items = this._items.filter((_, i) => i !== idx);
    this._filters = this._filters.filter((_, i) => i !== idx);
    const s = new Set([...this._open].map((n) => n > idx ? n - 1 : n).filter((n) => n !== idx));
    this._open = s;
    this.#commit();
  }

  #onFamilyChange(idx, fontFamily) {
    const fonts = this._fonts ?? [];
    const details = getFontDetails(fonts, fontFamily);
    const oldItem = this._items[idx];
    const styleExists = details.variants[oldItem.fontStyle] != null;
    const fontStyle = styleExists ? oldItem.fontStyle : "normal";
    const weights = details.variants[fontStyle] ?? ["400"];
    const fontWeight = weights.includes(oldItem.fontWeight) ? oldItem.fontWeight : "400";
    this.#updateItem(idx, { fontFamily, fontCategory: details.category, fontStyle, fontWeight });
    loadGoogleFont(fonts, fontFamily);
    this._filters = this._filters.map((f, i) => i === idx ? "" : f);
  }

  #onStyleChange(idx, fontStyle) {
    const fonts = this._fonts ?? [];
    const details = getFontDetails(fonts, this._items[idx].fontFamily);
    const weights = details.variants[fontStyle] ?? ["400"];
    const fontWeight = weights.includes(this._items[idx].fontWeight) ? this._items[idx].fontWeight : "400";
    this.#updateItem(idx, { fontStyle, fontWeight });
  }

  #onWeightChange(idx, fontWeight) {
    this.#updateItem(idx, { fontWeight });
  }

  #updateItem(idx, patch) {
    this._items = this._items.map((item, i) => i === idx ? { ...item, ...patch } : item);
    this.#commit();
  }

  #commit() {
    const next = JSON.stringify(this._items);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
      try { localStorage.setItem(FONTS_LS_KEY, next); } catch { /* ignore */ }
      try { new BroadcastChannel(FONTS_CHANNEL).postMessage({ type: "update", fonts: this._items }); } catch { /* ignore */ }
    }
  }

  // ── Drag reorder ─────────────────────────────────────────────────────────

  #onDragStart(idx, e) {
    this._dragging = idx;
    e.dataTransfer.effectAllowed = "move";
  }

  #onDragOver(idx, e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    if (this._dragOver !== idx) this._dragOver = idx;
  }

  #onDragLeave() {
    this._dragOver = null;
  }

  #onDrop(idx) {
    const from = this._dragging;
    this._dragging = null;
    this._dragOver = null;
    if (from === null || from === idx) return;
    const items = [...this._items];
    const filters = [...this._filters];
    const [item] = items.splice(from, 1);
    const [filt] = filters.splice(from, 1);
    items.splice(idx, 0, item);
    filters.splice(idx, 0, filt);
    // remap open set
    const openArr = [...this._open];
    const remapped = openArr.map((n) => {
      if (n === from) return idx;
      if (from < idx && n > from && n <= idx) return n - 1;
      if (from > idx && n >= idx && n < from) return n + 1;
      return n;
    });
    this._open = new Set(remapped);
    this._items = items;
    this._filters = filters;
    this.#commit();
  }

  #onDragEnd() {
    this._dragging = null;
    this._dragOver = null;
  }

  // ── Render ────────────────────────────────────────────────────────────────

  static styles = [
    UmbTextStyles,
    css`
      :host { display: block; }
      .s-fonts { max-width: 640px; }

      .fonts-intro { font-size: 12px; color: var(--uui-color-contrast-subdued, #6b7280); margin-bottom: 12px; }
      .fonts-intro a { color: var(--uui-color-interactive, #1b264f); }

      .acc-list { display: flex; flex-direction: column; gap: 6px; margin-bottom: 10px; }

      .acc-item {
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px; transition: border-color .15s;
      }
      .acc-item.drag-over { border-color: var(--uui-color-interactive, #1b264f); background: rgba(41,134,204,.04); }
      .acc-item.dragging { opacity: .4; }

      .acc-header {
        display: flex; align-items: center; gap: 8px;
        padding: 8px 10px;
        background: var(--uui-color-surface-alt, #f6f5f7);
        border-radius: 4px; cursor: pointer; user-select: none;
      }
      .acc-item.is-open .acc-header { border-radius: 4px 4px 0 0; border-bottom: 1px solid var(--uui-color-border, #d8d7d9); }

      .drag-handle {
        cursor: grab; color: var(--uui-color-contrast-subdued, #9ca3af);
        font-size: 14px; flex-shrink: 0; line-height: 1;
        padding: 2px 4px;
      }
      .drag-handle:active { cursor: grabbing; }

      .acc-chevron { font-size: 10px; color: var(--uui-color-contrast-subdued, #9ca3af); transition: transform .2s; flex-shrink: 0; }
      .acc-item.is-open .acc-chevron { transform: rotate(90deg); }

      .acc-title {
        flex: 1; font-size: 12px; font-weight: 500;
        color: var(--uui-color-contrast, #1b264f);
        white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
      }

      .del-btn {
        flex-shrink: 0; width: 22px; height: 22px; padding: 0;
        border: 1px solid rgba(0,0,0,.15); border-radius: 3px;
        background: #fff; cursor: pointer; font-size: 11px; line-height: 1;
        display: flex; align-items: center; justify-content: center;
        color: var(--uui-color-danger, #d42054);
      }
      .del-btn:disabled { opacity: .3; cursor: default; }
      .del-btn:not(:disabled):hover { border-color: var(--uui-color-danger, #d42054); background: #fff0f3; }

      .acc-body { display: none; padding: 14px 12px; }
      .acc-item.is-open .acc-body { display: block; }

      .body-layout { display: flex; gap: 16px; flex-wrap: wrap; }

      .controls { display: flex; flex-direction: column; gap: 10px; min-width: 200px; flex: 0 0 200px; }
      .field-group { display: flex; flex-direction: column; gap: 4px; }
      .field-label { font-size: 11px; font-weight: 600; color: var(--uui-color-contrast, #1b264f); }
      .field-filter {
        width: 100%; box-sizing: border-box;
        border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: 3px;
        padding: 3px 7px; font-size: 11px;
        background: var(--uui-color-surface, #fff);
      }
      .field-filter:focus { outline: none; border-color: var(--uui-color-interactive, #1b264f); }
      .field-select {
        width: 100%; box-sizing: border-box;
        border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: 3px;
        padding: 4px 6px; font-size: 12px;
        background: var(--uui-color-surface, #fff);
        max-height: 120px;
      }
      .field-select:focus { outline: none; border-color: var(--uui-color-interactive, #1b264f); }

      .preview-wrap {
        flex: 1; min-width: 200px;
        background: #d9d9d9; border-radius: 4px; padding: 4px;
      }
      .preview-inner {
        background: #fff; border-radius: 3px; padding: 12px 16px;
        font-size: 13px; line-height: 1.6;
      }
      .preview-inner p { margin: 0; }

      .add-btn {
        display: inline-flex; align-items: center; gap: 6px;
        padding: 6px 14px; border-radius: 4px;
        border: 1px dashed var(--uui-color-border, #d8d7d9);
        background: var(--uui-color-surface-alt, #f6f5f7);
        cursor: pointer; font-size: 12px;
        color: var(--uui-color-contrast, #1b264f);
      }
      .add-btn:hover { border-color: var(--uui-color-interactive, #1b264f); }

      .loading { font-size: 12px; color: var(--uui-color-contrast-subdued, #6b7280); padding: 4px 0; }
    `,
  ];

  #renderItem(item, idx) {
    const fonts = this._fonts ?? [];
    const details = getFontDetails(fonts, item.fontFamily);
    const styles = Object.keys(details.variants);
    const weights = details.variants[item.fontStyle] ?? ["400"];
    const filter = this._filters[idx] ?? "";
    const filteredFonts = filter
      ? fonts.filter((f) => f.family.toLowerCase().includes(filter.toLowerCase()))
      : fonts;

    const isOpen = this._open.has(idx);
    const isDragging = this._dragging === idx;
    const isDragOver = this._dragOver === idx;

    const titleStyle = `font-family:'${item.fontFamily}',${item.fontCategory};font-weight:${item.fontWeight};font-style:${item.fontStyle}`;
    const titleText = `${item.fontFamily}, ${details.category}, ${item.fontStyle}, ${item.fontWeight}`;

    const previewStyle = `font-family:'${item.fontFamily}',${item.fontCategory};font-weight:${item.fontWeight};font-style:${item.fontStyle}`;

    return html`
      <div
        class="acc-item ${isOpen ? "is-open" : ""} ${isDragging ? "dragging" : ""} ${isDragOver ? "drag-over" : ""}"
        draggable="true"
        @dragstart=${(e) => this.#onDragStart(idx, e)}
        @dragover=${(e) => this.#onDragOver(idx, e)}
        @dragleave=${() => this.#onDragLeave()}
        @drop=${() => this.#onDrop(idx)}
        @dragend=${() => this.#onDragEnd()}
      >
        <div class="acc-header" @click=${() => this.#toggleOpen(idx)}>
          <span class="drag-handle" @click=${(e) => e.stopPropagation()} title="Drag to reorder">⠿</span>
          <span class="acc-chevron">▶</span>
          <span class="acc-title" style=${titleStyle}>${titleText}</span>
          <button
            class="del-btn"
            ?disabled=${this._items.length <= 1}
            title="Delete"
            @click=${(e) => { e.stopPropagation(); this.#removeItem(idx); }}
          >✕</button>
        </div>
        <div class="acc-body">
          <div class="body-layout">
            <div class="controls">
              <div class="field-group">
                <label class="field-label">Font Family</label>
                ${this._fonts === null
                  ? html`<span class="loading">Loading fonts…</span>`
                  : html`
                    <input
                      class="field-filter"
                      type="text"
                      placeholder="Search…"
                      .value=${filter}
                      @input=${(e) => {
                        this._filters = this._filters.map((f, i) => i === idx ? e.target.value : f);
                      }}
                      @click=${(e) => e.stopPropagation()}
                    />
                    <select
                      class="field-select"
                      size="5"
                      .value=${item.fontFamily}
                      @change=${(e) => this.#onFamilyChange(idx, e.target.value)}
                      @click=${(e) => e.stopPropagation()}
                    >
                      ${filteredFonts.map((f) => html`
                        <option value=${f.family} ?selected=${f.family === item.fontFamily}>
                          ${f.family} (${f.category})
                        </option>
                      `)}
                    </select>
                  `}
              </div>
              <div class="field-group">
                <label class="field-label">Font Style</label>
                <select
                  class="field-select"
                  .value=${item.fontStyle}
                  @change=${(e) => this.#onStyleChange(idx, e.target.value)}
                  @click=${(e) => e.stopPropagation()}
                >
                  ${styles.map((s) => html`<option value=${s} ?selected=${s === item.fontStyle}>${s}</option>`)}
                </select>
              </div>
              <div class="field-group">
                <label class="field-label">Font Weight</label>
                <select
                  class="field-select"
                  .value=${item.fontWeight}
                  @change=${(e) => this.#onWeightChange(idx, e.target.value)}
                  @click=${(e) => e.stopPropagation()}
                >
                  ${weights.map((w) => html`<option value=${w} ?selected=${w === item.fontWeight}>${w}</option>`)}
                </select>
              </div>
            </div>
            <div class="preview-wrap">
              <div class="preview-inner" style=${previewStyle}>
                <p>The quick brown fox jumps over the lazy dog.</p>
                <p>Aa Bb Cc Dd Ee Ff Gg Hh Ii Jj Kk Ll Mm Nn Oo Pp Qq Rr Ss Tt Uu Vv Ww Xx Yy Zz</p>
                <p>0 1 2 3 4 5 6 7 8 9</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  render() {
    return html`
      <div class="s-fonts">
        <p class="fonts-intro">
          Fonts integrated from the Google Fonts library.
          Browse at <a href="https://fonts.google.com/" target="_blank">https://fonts.google.com</a>
        </p>
        <div class="acc-list">
          ${this._items.map((item, idx) => this.#renderItem(item, idx))}
        </div>
        <button type="button" class="add-btn" @click=${this.#addItem}>
          <span>+</span> Add Font Setting
        </button>
      </div>
    `;
  }
}

if (!customElements.get("dcms-fonts-property-editor")) {
  customElements.define("dcms-fonts-property-editor", DcmsFontsPropertyEditor);
}
