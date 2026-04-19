import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const WEBFONTS_URL = "/App_Plugins/DcmsV16/font-picker/webfonts.json";
const GOOGLE_FONTS_BASE = "https://fonts.googleapis.com/css?family=";

const ELEMENTS = ["text", "h1", "h2", "h3", "h4", "h5", "h6", "button"];

const PREVIEW_TEXT = {
  text: "The quick brown fox jumps over the lazy dog.",
  h1: "This is the H1 header", h2: "This is the H2 header",
  h3: "This is the H3 header", h4: "This is the H4 header",
  h5: "This is the H5 header", h6: "This is the H6 header",
  button: "Large Button  ·  Normal Button  ·  Small Button",
};

function titleCase(s) {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

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

const _loadedFonts = new Set();
function loadGoogleFont(fonts, fontFamily) {
  if (_loadedFonts.has(fontFamily)) return;
  _loadedFonts.add(fontFamily);
  const details = getFontDetails(fonts, fontFamily);
  const parts = [];
  for (const [style, weights] of Object.entries(details.variants)) {
    const suffix = style === "normal" ? "" : style[0];
    parts.push(...weights.map((w) => w + suffix));
  }
  const url = GOOGLE_FONTS_BASE + encodeURIComponent(fontFamily).replace(/%20/g, "+") + ":" + parts.join(",") + "&display=swap";
  if (!document.querySelector(`link[href="${url}"]`)) {
    const link = document.createElement("link");
    link.rel = "stylesheet"; link.href = url;
    document.head.appendChild(link);
  }
}

function makeFont(fontFamily = "Poppins", fontCategory = "sans-serif", fontStyle = "normal", fontWeight = "400") {
  return { fontFamily, fontCategory, fontStyle, fontWeight };
}

const DEFAULT_ITEMS = [
  { element: "text",   font: makeFont("Poppins", "sans-serif", "normal", "400"), desktop: 16, tablet: 16, mobile: 16 },
  { element: "h1",     font: makeFont("DM Sans",  "sans-serif", "normal", "700"), desktop: 54, tablet: 48, mobile: 40 },
  { element: "h2",     font: makeFont("DM Sans",  "sans-serif", "normal", "700"), desktop: 46, tablet: 34, mobile: 34 },
  { element: "h3",     font: makeFont("DM Sans",  "sans-serif", "normal", "500"), desktop: 32, tablet: 32, mobile: 32 },
  { element: "h4",     font: makeFont("DM Sans",  "sans-serif", "normal", "500"), desktop: 26, tablet: 26, mobile: 26 },
  { element: "h5",     font: makeFont("DM Sans",  "sans-serif", "normal", "500"), desktop: 22, tablet: 22, mobile: 22 },
  { element: "h6",     font: makeFont("DM Sans",  "sans-serif", "normal", "700"), desktop: 18, tablet: 18, mobile: 18 },
  { element: "button", font: makeFont("Poppins",  "sans-serif", "normal", "400"), desktop: 16, tablet: 16, mobile: 16 },
];

function parseValue(raw) {
  try {
    const arr = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (Array.isArray(arr)) {
      return ELEMENTS.map((el) => {
        const saved = arr.find((x) => x?.element === el);
        const def = DEFAULT_ITEMS.find((d) => d.element === el);
        if (!saved) return { ...def, font: { ...def.font } };
        return {
          element: el,
          font: { ...def.font, ...(saved.font ?? {}) },
          desktop: saved.desktop ?? def.desktop,
          tablet: saved.tablet ?? def.tablet,
          mobile: saved.mobile ?? def.mobile,
        };
      });
    }
  } catch { /* fall through */ }
  return DEFAULT_ITEMS.map((x) => ({ ...x, font: { ...x.font } }));
}

export default class DcmsDesignTypographyPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _items: { state: true },
    _open: { state: true },    // Set<number>
    _fonts: { state: true },   // WebfontItem[] | null
    _filters: { state: true }, // string[] per item
    _device: { state: true },  // "desktop" | "tablet" | "mobile"
  };

  constructor() {
    super();
    this.value = undefined;
    this._items = DEFAULT_ITEMS.map((x) => ({ ...x, font: { ...x.font } }));
    this._open = new Set([0]);
    this._fonts = null;
    this._filters = ELEMENTS.map(() => "");
    this._device = "desktop";
  }

  connectedCallback() {
    super.connectedCallback();
    this._items = parseValue(this.value);
    this.#loadFonts();
  }

  willUpdate(/** @type {Map<string,unknown>} */ changed) {
    if (changed.has("value")) this._items = parseValue(this.value);
  }

  async #loadFonts() {
    try {
      const res = await fetch(WEBFONTS_URL);
      const data = await res.json();
      this._fonts = data.items ?? [];
      for (const item of this._items) {
        loadGoogleFont(this._fonts, item.font.fontFamily);
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

  #onFamilyChange(idx, fontFamily) {
    const fonts = this._fonts ?? [];
    const details = getFontDetails(fonts, fontFamily);
    const old = this._items[idx].font;
    const styleExists = details.variants[old.fontStyle] != null;
    const fontStyle = styleExists ? old.fontStyle : "normal";
    const weights = details.variants[fontStyle] ?? ["400"];
    const fontWeight = weights.includes(old.fontWeight) ? old.fontWeight : "400";
    this.#patchFont(idx, { fontFamily, fontCategory: details.category, fontStyle, fontWeight });
    loadGoogleFont(fonts, fontFamily);
    this._filters = this._filters.map((f, i) => i === idx ? "" : f);
  }

  #onStyleChange(idx, fontStyle) {
    const fonts = this._fonts ?? [];
    const details = getFontDetails(fonts, this._items[idx].font.fontFamily);
    const weights = details.variants[fontStyle] ?? ["400"];
    const fontWeight = weights.includes(this._items[idx].font.fontWeight) ? this._items[idx].font.fontWeight : "400";
    this.#patchFont(idx, { fontStyle, fontWeight });
  }

  #onWeightChange(idx, fontWeight) {
    this.#patchFont(idx, { fontWeight });
  }

  #patchFont(idx, patch) {
    this._items = this._items.map((item, i) =>
      i !== idx ? item : { ...item, font: { ...item.font, ...patch } }
    );
    this.#commit();
  }

  #onSizeInput(idx, device, val) {
    const n = parseInt(val, 10);
    if (isNaN(n) || n < 1 || n > 999) return;
    this._items = this._items.map((item, i) =>
      i !== idx ? item : { ...item, [device]: n }
    );
    this.#commit();
  }

  #commit() {
    const next = JSON.stringify(this._items);
    if (next !== this.value) { this.value = next; this.dispatchEvent(new UmbChangeEvent()); }
  }

  // ── Styles ────────────────────────────────────────────────────────────────

  static styles = [
    UmbTextStyles,
    css`
      :host { display: block; }
      .s-typo { max-width: 680px; }

      .device-bar {
        display: flex; gap: 4px; margin-bottom: 10px; align-items: center;
      }
      .device-label { font-size: 11px; color: var(--uui-color-contrast-subdued, #6b7280); margin-right: 6px; }
      .device-btn {
        padding: 3px 10px; border-radius: 3px; font-size: 11px; font-weight: 500;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        background: var(--uui-color-surface-alt, #f6f5f7);
        cursor: pointer; color: var(--uui-color-contrast, #1b264f);
      }
      .device-btn.is-active {
        background: var(--uui-color-interactive, #1b264f);
        color: #fff; border-color: transparent;
      }

      .acc-list { display: flex; flex-direction: column; gap: 6px; }
      .acc-item {
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px;
      }
      .acc-header {
        display: flex; align-items: center; gap: 10px;
        padding: 8px 12px;
        background: var(--uui-color-surface-alt, #f6f5f7);
        border-radius: 4px; cursor: pointer; user-select: none;
      }
      .acc-item.is-open .acc-header {
        border-radius: 4px 4px 0 0;
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
      }
      .acc-chevron { font-size: 10px; color: var(--uui-color-contrast-subdued, #9ca3af); transition: transform .2s; flex-shrink: 0; }
      .acc-item.is-open .acc-chevron { transform: rotate(90deg); }
      .acc-tag {
        min-width: 44px; padding: 1px 7px; border-radius: 3px; text-align: center;
        font-size: 11px; font-weight: 700; flex-shrink: 0;
        background: var(--uui-color-interactive, #1b264f); color: #fff;
      }
      .acc-title {
        flex: 1; font-size: 13px;
        white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
      }
      .acc-meta {
        font-size: 10px; color: var(--uui-color-contrast-subdued, #6b7280); flex-shrink: 0;
      }

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

      .size-row { display: flex; gap: 8px; align-items: flex-end; flex-wrap: wrap; }
      .size-field { display: flex; flex-direction: column; gap: 3px; }
      .size-label {
        font-size: 10px; font-weight: 600; color: var(--uui-color-contrast-subdued, #6b7280);
        text-align: center;
      }
      .size-input {
        width: 54px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: 3px;
        padding: 4px 6px; font-size: 12px; text-align: center;
        background: var(--uui-color-surface, #fff);
        box-sizing: border-box;
      }
      .size-input:focus { outline: none; border-color: var(--uui-color-interactive, #1b264f); }

      .preview-wrap {
        flex: 1; min-width: 200px;
        background: #d9d9d9; border-radius: 4px; padding: 4px;
      }
      .preview-inner {
        background: #fff; border-radius: 3px; padding: 16px;
        word-break: break-word; overflow: hidden;
      }

      .loading { font-size: 12px; color: var(--uui-color-contrast-subdued, #6b7280); }
    `,
  ];

  // ── Render ────────────────────────────────────────────────────────────────

  #renderItem(item, idx) {
    const fonts = this._fonts ?? [];
    const details = getFontDetails(fonts, item.font.fontFamily);
    const styles = Object.keys(details.variants);
    const weights = details.variants[item.font.fontStyle] ?? ["400"];
    const filter = this._filters[idx] ?? "";
    const filteredFonts = filter
      ? fonts.filter((f) => f.family.toLowerCase().includes(filter.toLowerCase()))
      : fonts;

    const isOpen = this._open.has(idx);
    const titleStyle = `font-family:'${item.font.fontFamily}',${item.font.fontCategory};font-weight:${item.font.fontWeight};font-style:${item.font.fontStyle}`;
    const previewSize = item[this._device] ?? item.desktop;
    const previewStyle = `${titleStyle};font-size:${previewSize}px;line-height:${item.element === "text" ? 1.5 : 1.2}`;

    return html`
      <div class="acc-item ${isOpen ? "is-open" : ""}">
        <div class="acc-header" @click=${() => this.#toggleOpen(idx)}>
          <span class="acc-chevron">▶</span>
          <span class="acc-tag">${item.element}</span>
          <span class="acc-title" style=${titleStyle}>${titleCase(item.element)} — ${item.font.fontFamily} ${item.font.fontWeight}</span>
          <span class="acc-meta">${item.desktop}/${item.tablet}/${item.mobile}px</span>
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
                      @input=${(e) => { this._filters = this._filters.map((f, i) => i === idx ? e.target.value : f); }}
                      @click=${(e) => e.stopPropagation()}
                    />
                    <select
                      class="field-select"
                      size="5"
                      .value=${item.font.fontFamily}
                      @change=${(e) => this.#onFamilyChange(idx, e.target.value)}
                      @click=${(e) => e.stopPropagation()}
                    >
                      ${filteredFonts.map((f) => html`
                        <option value=${f.family} ?selected=${f.family === item.font.fontFamily}>
                          ${f.family} (${f.category})
                        </option>
                      `)}
                    </select>
                  `}
              </div>
              <div class="field-group">
                <label class="field-label">Font Style</label>
                <select class="field-select" .value=${item.font.fontStyle}
                  @change=${(e) => this.#onStyleChange(idx, e.target.value)}
                  @click=${(e) => e.stopPropagation()}>
                  ${styles.map((s) => html`<option value=${s} ?selected=${s === item.font.fontStyle}>${s}</option>`)}
                </select>
              </div>
              <div class="field-group">
                <label class="field-label">Font Weight</label>
                <select class="field-select" .value=${item.font.fontWeight}
                  @change=${(e) => this.#onWeightChange(idx, e.target.value)}
                  @click=${(e) => e.stopPropagation()}>
                  ${weights.map((w) => html`<option value=${w} ?selected=${w === item.font.fontWeight}>${w}</option>`)}
                </select>
              </div>
              <div class="field-group">
                <label class="field-label">Size (px)</label>
                <div class="size-row">
                  ${["desktop","tablet","mobile"].map((d) => html`
                    <div class="size-field">
                      <span class="size-label">${d.charAt(0).toUpperCase()}</span>
                      <input
                        class="size-input"
                        type="number"
                        min="1" max="999"
                        .value=${String(item[d])}
                        @change=${(e) => this.#onSizeInput(idx, d, e.target.value)}
                        @click=${(e) => e.stopPropagation()}
                      />
                    </div>
                  `)}
                </div>
              </div>
            </div>
            <div class="preview-wrap">
              <div class="preview-inner" style=${previewStyle}>
                ${PREVIEW_TEXT[item.element]}
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  render() {
    return html`
      <div class="s-typo">
        <div class="device-bar">
          <span class="device-label">Preview size:</span>
          ${["desktop","tablet","mobile"].map((d) => html`
            <button
              class="device-btn ${this._device === d ? "is-active" : ""}"
              @click=${() => { this._device = d; }}
            >${titleCase(d)}</button>
          `)}
        </div>
        <div class="acc-list">
          ${this._items.map((item, idx) => this.#renderItem(item, idx))}
        </div>
      </div>
    `;
  }
}

if (!customElements.get("dcms-design-typography-property-editor")) {
  customElements.define("dcms-design-typography-property-editor", DcmsDesignTypographyPropertyEditor);
}
