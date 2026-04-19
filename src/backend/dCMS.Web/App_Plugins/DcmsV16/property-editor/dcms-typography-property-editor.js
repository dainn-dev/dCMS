import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const FONTS_CHANNEL = "dcms_fonts_v1";
const FONTS_LS_KEY = "dcms.fonts.v1";

const PREVIEW_TEXT = {
  text: "Lorem ipsum futuristic city 2047, Jane & John discovered a secret facility.",
  h1: "This is the H1 header",
  h2: "This is the H2 header",
  h3: "This is the H3 header",
  h4: "This is the H4 header",
  h5: "This is the H5 header",
  h6: "This is the H6 header",
  button: "Large Button — Normal Button — Small Button",
};

const ELEMENTS = ["text", "h1", "h2", "h3", "h4", "h5", "h6", "button"];

const DEFAULT_FONTS = [
  { fontFamily: "Poppins", fontCategory: "sans-serif", fontStyle: "normal", fontWeight: "400" },
  { fontFamily: "DM Sans",  fontCategory: "sans-serif", fontStyle: "normal", fontWeight: "500" },
  { fontFamily: "DM Sans",  fontCategory: "sans-serif", fontStyle: "normal", fontWeight: "700" },
];

const DEFAULT_ROWS = [
  { element: "text",   font: DEFAULT_FONTS[0], desktop: 16, tablet: 16, mobile: 16 },
  { element: "h1",     font: DEFAULT_FONTS[2], desktop: 54, tablet: 48, mobile: 40 },
  { element: "h2",     font: DEFAULT_FONTS[2], desktop: 46, tablet: 34, mobile: 34 },
  { element: "h3",     font: DEFAULT_FONTS[1], desktop: 32, tablet: 32, mobile: 32 },
  { element: "h4",     font: DEFAULT_FONTS[1], desktop: 26, tablet: 26, mobile: 26 },
  { element: "h5",     font: DEFAULT_FONTS[1], desktop: 22, tablet: 22, mobile: 22 },
  { element: "h6",     font: DEFAULT_FONTS[2], desktop: 18, tablet: 18, mobile: 18 },
  { element: "button", font: DEFAULT_FONTS[0], desktop: 16, tablet: 16, mobile: 16 },
];

function fontKey(f) { return `${f.fontFamily}::${f.fontStyle}::${f.fontWeight}`; }

function findFont(fonts, f) {
  if (!f || !fonts.length) return fonts[0] ?? DEFAULT_FONTS[0];
  const k = fontKey(f);
  return fonts.find(x => fontKey(x) === k) ?? fonts[0];
}

function loadFontsFromStorage() {
  try {
    const raw = localStorage.getItem(FONTS_LS_KEY);
    if (!raw) return [...DEFAULT_FONTS];
    const arr = JSON.parse(raw);
    return Array.isArray(arr) && arr.length ? arr : [...DEFAULT_FONTS];
  } catch { return [...DEFAULT_FONTS]; }
}

function parseValue(raw, fonts) {
  const fallbackFonts = fonts.length ? fonts : DEFAULT_FONTS;
  try {
    const arr = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (Array.isArray(arr) && arr.length) {
      return ELEMENTS.map(el => {
        const def = DEFAULT_ROWS.find(r => r.element === el);
        const saved = arr.find(x => x.element === el);
        if (!saved) return { ...def, font: findFont(fallbackFonts, def.font) };
        return {
          element: el,
          font: findFont(fallbackFonts, saved.font),
          desktop: saved.desktop ?? def.desktop,
          tablet:  saved.tablet  ?? def.tablet,
          mobile:  saved.mobile  ?? def.mobile,
        };
      });
    }
  } catch { /* fall through */ }
  return DEFAULT_ROWS.map(r => ({ ...r, font: findFont(fallbackFonts, r.font) }));
}

function loadGoogleFont(fontFamily) {
  const key = `dcms-gf-${fontFamily}`;
  if (document.head.querySelector(`link[data-dcms-font="${key}"]`)) return;
  const url = `https://fonts.googleapis.com/css?family=${encodeURIComponent(fontFamily).replace(/%20/g, "+")}&display=swap`;
  const link = Object.assign(document.createElement("link"), { rel: "stylesheet", href: url });
  link.dataset.dcmsFont = key;
  document.head.appendChild(link);
}

export default class DcmsTypographyPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _rows:  { state: true },
    _fonts: { state: true },
    _open:  { state: true },
  };

  #bc = null;

  constructor() {
    super();
    this.value = undefined;
    this._fonts = [...DEFAULT_FONTS];
    this._rows  = DEFAULT_ROWS.map(r => ({ ...r }));
    this._open  = new Set([0]);
  }

  connectedCallback() {
    super.connectedCallback();
    this._fonts = loadFontsFromStorage();
    this._rows  = parseValue(this.value, this._fonts);
    this._rows.forEach(r => loadGoogleFont(r.font.fontFamily));
    try {
      this.#bc = new BroadcastChannel(FONTS_CHANNEL);
      this.#bc.onmessage = (e) => {
        if (e.data?.type === "update" && Array.isArray(e.data.fonts)) {
          this._fonts = e.data.fonts;
          this._rows  = this._rows.map(r => ({ ...r, font: findFont(this._fonts, r.font) }));
          this._rows.forEach(r => loadGoogleFont(r.font.fontFamily));
        }
      };
    } catch { /* ignore */ }
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#bc?.close();
    this.#bc = null;
  }

  willUpdate(changed) {
    if (changed.has("value")) {
      this._rows = parseValue(this.value, this._fonts);
    }
  }

  // ── Handlers ────────────────────────────────────────────────────────────────

  #toggle(idx) {
    const s = new Set(this._open);
    s.has(idx) ? s.delete(idx) : s.add(idx);
    this._open = s;
  }

  #setFont(idx, fontIdx) {
    const font = this._fonts[parseInt(fontIdx)] ?? this._fonts[0];
    loadGoogleFont(font.fontFamily);
    this._rows = this._rows.map((r, i) => i === idx ? { ...r, font } : r);
    this.#commit();
  }

  #setSize(idx, device, val) {
    const n = Math.max(1, Math.min(999, parseInt(val) || 1));
    this._rows = this._rows.map((r, i) => i === idx ? { ...r, [device]: n } : r);
    this.#commit();
  }

  #commit() {
    const next = JSON.stringify(this._rows);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  // ── Helpers ──────────────────────────────────────────────────────────────────

  #fontIdx(font) {
    const k = fontKey(font);
    const i = this._fonts.findIndex(f => fontKey(f) === k);
    return i === -1 ? 0 : i;
  }

  #fs(font, sizePx) {
    return `font-family:'${font.fontFamily}',${font.fontCategory};font-weight:${font.fontWeight};font-style:${font.fontStyle};font-size:${sizePx}px;line-height:1.3`;
  }

  // ── Render ───────────────────────────────────────────────────────────────────

  render() {
    return html`<div class="typ-editor">${this._rows.map((r, i) => this.#row(r, i))}</div>`;
  }

  #row(row, idx) {
    const open    = this._open.has(idx);
    const isBtn   = row.element === "button";
    const capSize = Math.min(row.desktop, 22);
    return html`
      <div class="typ-row">
        <div class="typ-head" @click=${() => this.#toggle(idx)}>
          <span class="el-badge">${row.element === "text" ? "P" : row.element.toUpperCase()}</span>
          <span class="el-name" style=${this.#fs(row.font, capSize)}>${row.element}</span>
          ${!isBtn ? html`<span class="sizes">${row.desktop}/${row.tablet}/${row.mobile}px</span>` : ""}
          <span class="chev">${open ? "▲" : "▼"}</span>
        </div>
        ${open ? this.#body(row, idx, isBtn) : ""}
      </div>
    `;
  }

  #body(row, idx, isBtn) {
    const selIdx = this.#fontIdx(row.font);
    return html`
      <div class="typ-body">
        <div class="controls">
          <div class="ctrl-row">
            <label>Font</label>
            <select @change=${e => this.#setFont(idx, e.target.value)}>
              ${this._fonts.map((f, fi) => html`
                <option value=${fi} ?selected=${fi === selIdx}>
                  ${f.fontFamily}, ${f.fontWeight}, ${f.fontStyle}
                </option>
              `)}
            </select>
          </div>
          ${!isBtn ? ["desktop", "tablet", "mobile"].map(dev => html`
            <div class="ctrl-row ctrl-size">
              <label>${dev[0].toUpperCase() + dev.slice(1)}</label>
              <div class="size-wrap">
                <input type="number" min="1" max="999" .value=${String(row[dev])}
                  @change=${e => this.#setSize(idx, dev, e.target.value)} />
                <span class="unit">px</span>
              </div>
            </div>
          `) : ""}
        </div>
        <div class="preview-panel">
          ${["desktop", "tablet", "mobile"].map(dev => html`
            <div class="prev-row">
              <span class="dev-lbl">${dev[0].toUpperCase() + dev.slice(1)}</span>
              <div class="prev-text" style=${this.#fs(row.font, row[dev])}>
                ${PREVIEW_TEXT[row.element] ?? row.element}
              </div>
            </div>
          `)}
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .typ-editor { display: flex; flex-direction: column; gap: 4px; }
    .typ-row { border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 6px; overflow: hidden; }
    .typ-head {
      display: flex; align-items: center; gap: 10px; padding: 10px 14px;
      cursor: pointer; background: var(--uui-color-surface, #fff); user-select: none;
    }
    .typ-head:hover { background: var(--uui-color-surface-alt, #f5f5f5); }
    .el-badge {
      font-size: 10px; font-weight: 700; padding: 2px 7px; border-radius: 3px;
      background: var(--uui-color-current, #3544b1); color: #fff; flex-shrink: 0;
    }
    .el-name { flex: 1; min-width: 0; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .sizes { font-size: 11px; color: var(--uui-color-text-alt, #767676); flex-shrink: 0; }
    .chev { font-size: 10px; color: var(--uui-color-text-alt, #767676); flex-shrink: 0; }
    .typ-body {
      display: flex; gap: 16px; padding: 14px;
      border-top: 1px solid var(--uui-color-border, #e3e3e3);
      background: var(--uui-color-surface-alt, #f5f5f5);
    }
    .controls { display: flex; flex-direction: column; gap: 10px; min-width: 200px; flex-shrink: 0; }
    .ctrl-row { display: flex; flex-direction: column; gap: 3px; }
    .ctrl-row label { font-size: 12px; font-weight: 600; }
    select {
      padding: 6px 8px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; font-size: 13px; background: #fff; width: 100%;
    }
    .ctrl-size { flex-direction: row; align-items: center; gap: 8px; }
    .ctrl-size label { width: 60px; flex-shrink: 0; margin: 0; }
    .size-wrap { display: flex; align-items: center; gap: 4px; }
    .size-wrap input {
      width: 64px; padding: 5px 8px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; font-size: 13px;
    }
    .unit { font-size: 12px; color: var(--uui-color-text-alt, #767676); }
    .preview-panel {
      flex: 1; min-width: 0; background: #d9d9d9; padding: 12px; border-radius: 4px;
      display: flex; flex-direction: column; gap: 14px; overflow: hidden;
    }
    .prev-row { display: flex; flex-direction: column; gap: 2px; }
    .dev-lbl { font-size: 10px; font-weight: 700; color: #555; text-transform: uppercase; letter-spacing: .5px; }
    .prev-text { word-break: break-word; color: #1b1b1f; }
  `];
}

customElements.define("dcms-typography-property-editor", DcmsTypographyPropertyEditor);
