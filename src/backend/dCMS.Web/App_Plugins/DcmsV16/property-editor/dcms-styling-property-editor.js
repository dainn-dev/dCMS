import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const ACP_JS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.js";
const ACP_CSS = "/App_Plugins/DcmsV16/color-picker/acolorpicker.min.css";

// ── Field definitions ────────────────────────────────────────────────────────

const CONTENT_FIELDS = [
  { key: "background", label: "Block Background" },
  { key: "h1", label: "H1" }, { key: "h2", label: "H2" },
  { key: "h3", label: "H3" }, { key: "h4", label: "H4" },
  { key: "h5", label: "H5" }, { key: "h6", label: "H6" },
  { key: "primary", label: "Body primary" }, { key: "secondary", label: "Body secondary" },
  { key: "border", label: "Border" }, { key: "link", label: "Link" }, { key: "linkHover", label: "Link Hover" },
];
const BUTTON_FIELDS = [
  { key: "text", label: "Text" }, { key: "textHover", label: "Text Hover" },
  { key: "fill", label: "Fill" }, { key: "fillHover", label: "Fill Hover" },
  { key: "stroke", label: "Stroke" }, { key: "strokeHover", label: "Stroke Hover" },
];
const BTN_SEC_FIELDS = [
  { key: "text", label: "Text" }, { key: "textHover", label: "Text Hover" },
  { key: "fillHover", label: "Fill Hover" },
  { key: "stroke", label: "Stroke" }, { key: "strokeHover", label: "Stroke Hover" },
];

const SECTIONS = [
  { id: "content", label: "Content", fields: CONTENT_FIELDS },
  { id: "button", label: "Primary Button", fields: BUTTON_FIELDS },
  { id: "buttonSecondary", label: "Secondary Button", fields: BTN_SEC_FIELDS },
];

// ── Value helpers ─────────────────────────────────────────────────────────────

const DEFAULT_SCHEME = {
  content: {
    background: "ffffff", h1: "000000", h2: "000000", h3: "000000",
    h4: "000000", h5: "000000", h6: "bcbcbc",
    primary: "5b5b5b", secondary: "5b5b5b", border: "5b5b5b",
    link: "2986cc", linkHover: "bcbcbc",
  },
  button: {
    text: "ffffff", textHover: "ffffff",
    fill: "2986cc", fillHover: "174a71",
    stroke: "2986cc", strokeHover: "174a71",
  },
  buttonSecondary: {
    text: "2986cc", textHover: "174a71",
    fillHover: "ffffff", stroke: "2986cc", strokeHover: "174a71",
  },
};

function cloneScheme(s) { return JSON.parse(JSON.stringify(s)); }

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
    const arr = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (Array.isArray(arr)) return arr.map(mergeScheme);
  } catch { /* fall through */ }
  return [];
}

/** stored hex (no #) → "#rrggbb" for CSS */
function toDisplay(raw) { return "#" + String(raw ?? "000000").replace(/^#/, "").slice(0, 6).padEnd(6, "0"); }
/** any hex input → 6-char no-# storage format */
function toStorage(raw) { return String(raw ?? "").replace(/^#/, "").slice(0, 6).toLowerCase() || "000000"; }

// ── AColorPicker loader ───────────────────────────────────────────────────────

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

// ── Component ─────────────────────────────────────────────────────────────────

export default class DcmsStylingPropertyEditor extends LitElement {
  static properties = {
    value: { type: String },
    _items: { state: true },
    _open: { state: true },    // Set<number> — expanded scheme indices
    _secOpen: { state: true }, // Set<string> — "idx-section" expanded sub-sections
    _active: { state: true },  // { itemIdx, section, key, label } | null
  };

  constructor() {
    super();
    this.value = undefined;
    this._items = [];
    this._open = new Set();
    this._secOpen = new Set();
    this._active = null;
    /** @type {any} */ this._picker = null;
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

  async #mountOrUpdate(displayHex) {
    await this.updateComplete;
    await ensureAColorPicker();
    if (!window.AColorPicker) return;

    if (!this.shadowRoot?.querySelector("link#dcms-acp-sty")) {
      const link = document.createElement("link");
      link.id = "dcms-acp-sty"; link.rel = "stylesheet"; link.href = ACP_CSS;
      this.shadowRoot?.appendChild(link);
    }

    const host = this.renderRoot.querySelector("[data-acp]");
    if (!host) return;

    if (this._pickerMounted) { this._picker?.[0]?.setColor(displayHex); return; }

    host.setAttribute("acp-color", displayHex);
    host.setAttribute("acp-show-alpha", "no");
    host.setAttribute("acp-show-rgb", "no");
    host.setAttribute("acp-show-hsl", "no");

    this._picker = window.AColorPicker.from(host, { hueBarSize: [230, 140], slBarSize: [230, 140] });
    this._picker.on("change", (/** @type {any} */ p) => {
      if (!this._active) return;
      this.#applyColor(this._active.itemIdx, this._active.section, this._active.key, toStorage(p?.all?.hexcss4));
    });
    this._pickerMounted = true;
  }

  #onDocClick(/** @type {MouseEvent} */ e) {
    if (!this._active) return;
    if (e.composedPath().includes(this)) return;
    this._active = null;
    this.#teardown();
  }

  // ── Scheme management ─────────────────────────────────────────────────────

  #addScheme() {
    this._items = [...this._items, cloneScheme(DEFAULT_SCHEME)];
    const idx = this._items.length - 1;
    this._open = new Set([...this._open, idx]);
    this.#commit();
  }

  #copyScheme(/** @type {number} */ idx) {
    const items = [...this._items.slice(0, idx + 1), cloneScheme(this._items[idx]), ...this._items.slice(idx + 1)];
    this._items = items;
    // remap open indices > idx
    this._open = new Set([...this._open].map((n) => n > idx ? n + 1 : n));
    this._secOpen = new Set([...this._secOpen].map((k) => {
      const [n, sec] = k.split("-");
      const ni = parseInt(n);
      return ni > idx ? `${ni + 1}-${sec}` : k;
    }));
    this.#commit();
  }

  #deleteScheme(/** @type {number} */ idx) {
    this._items = this._items.filter((_, i) => i !== idx);
    if (this._active?.itemIdx === idx) { this._active = null; this.#teardown(); }
    this._open = new Set([...this._open].filter((n) => n !== idx).map((n) => n > idx ? n - 1 : n));
    this._secOpen = new Set(
      [...this._secOpen]
        .filter((k) => parseInt(k.split("-")[0]) !== idx)
        .map((k) => { const [n, sec] = k.split("-"); const ni = parseInt(n); return ni > idx ? `${ni - 1}-${sec}` : k; })
    );
    this.#commit();
  }

  #toggleScheme(/** @type {number} */ idx) {
    const s = new Set(this._open);
    s.has(idx) ? s.delete(idx) : s.add(idx);
    this._open = s;
    if (!this._open.has(idx) && this._active?.itemIdx === idx) { this._active = null; this.#teardown(); }
  }

  #toggleSection(/** @type {number} */ itemIdx, /** @type {string} */ secId) {
    const key = `${itemIdx}-${secId}`;
    const s = new Set(this._secOpen);
    s.has(key) ? s.delete(key) : s.add(key);
    this._secOpen = s;
    if (!s.has(key) && this._active?.itemIdx === itemIdx && this._active?.section === secId) {
      this._active = null;
      this.#teardown();
    }
  }

  #applyColor(/** @type {number} */ itemIdx, /** @type {string} */ section, /** @type {string} */ key, /** @type {string} */ stored) {
    this._items = this._items.map((item, i) =>
      i !== itemIdx ? item : { ...item, [section]: { ...item[section], [key]: stored } }
    );
    this.#commit();
  }

  #activateField(/** @type {number} */ itemIdx, /** @type {string} */ section, /** @type {string} */ key, /** @type {string} */ label, /** @type {MouseEvent} */ e) {
    e.stopPropagation();
    if (this._active?.itemIdx === itemIdx && this._active?.section === section && this._active?.key === key) {
      this._active = null; this.#teardown(); return;
    }
    this._active = { itemIdx, section, key, label };
    this.#mountOrUpdate(toDisplay(this._items[itemIdx]?.[section]?.[key] ?? "000000"));
  }

  #onHexInput(/** @type {InputEvent} */ e) {
    if (!this._active) return;
    const raw = /** @type {HTMLInputElement} */ (e.target).value;
    if (!/^#[0-9a-fA-F]{6}$/.test(raw)) return;
    const stored = toStorage(raw);
    this.#applyColor(this._active.itemIdx, this._active.section, this._active.key, stored);
    this._picker?.[0]?.setColor(raw.toLowerCase());
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
      .s-sty { max-width: 680px; }

      /* Scheme accordion */
      .scheme-list { display: flex; flex-direction: column; gap: 8px; margin-bottom: 10px; }
      .scheme-item { border: 1px solid var(--uui-color-border,#d8d7d9); border-radius: 6px; }
      .scheme-header {
        display: flex; align-items: center; gap: 8px; padding: 8px 12px;
        background: var(--uui-color-surface-alt,#f6f5f7); border-radius: 6px;
        cursor: pointer; user-select: none;
      }
      .scheme-item.is-open .scheme-header { border-radius: 6px 6px 0 0; border-bottom: 1px solid var(--uui-color-border,#d8d7d9); }
      .scheme-chevron { font-size: 10px; color: var(--uui-color-contrast-subdued,#9ca3af); transition: transform .2s; flex-shrink: 0; }
      .scheme-item.is-open .scheme-chevron { transform: rotate(90deg); }
      .scheme-swatch { width: 20px; height: 20px; border-radius: 3px; border: 1px solid rgba(0,0,0,.12); flex-shrink: 0; }
      .scheme-title { flex: 1; font-size: 13px; font-weight: 600; color: var(--uui-color-contrast,#1b264f); }
      .scheme-actions { display: flex; gap: 4px; }
      .hdr-btn {
        width: 22px; height: 22px; border-radius: 3px;
        border: 1px solid rgba(0,0,0,.15); background: #fff;
        display: flex; align-items: center; justify-content: center;
        cursor: pointer; font-size: 11px; padding: 0; line-height: 1;
      }
      .hdr-btn:hover { border-color: #333; }
      .hdr-btn.del:hover { border-color: var(--uui-color-danger,#d42054); color: var(--uui-color-danger,#d42054); background: #fff0f3; }

      .scheme-body { display: none; }
      .scheme-item.is-open .scheme-body { display: block; }

      /* Inner layout: color grid + preview side by side */
      .content-row { display: flex; gap: 16px; flex-wrap: wrap; padding: 12px; }
      .color-panel { flex: 0 0 auto; min-width: 180px; }
      .preview-panel { flex: 1; min-width: 220px; }

      /* Color grid */
      .color-grid { display: flex; flex-wrap: wrap; gap: 8px 14px; }
      .color-field { display: flex; flex-direction: column; align-items: center; gap: 4px; cursor: pointer; }
      .field-swatch {
        width: 30px; height: 30px; border-radius: 4px;
        border: 2px solid transparent; box-shadow: 0 0 0 1px rgba(0,0,0,.15);
        transition: border-color .15s, box-shadow .15s;
      }
      .color-field:hover .field-swatch { box-shadow: 0 0 0 1px var(--uui-color-interactive,#1b264f); }
      .color-field.is-active .field-swatch { border-color: var(--uui-color-interactive,#1b264f); box-shadow: 0 0 0 3px rgba(41,134,204,.25); }
      .field-label { font-size: 10px; color: var(--uui-color-contrast-subdued,#6b7280); text-align: center; max-width: 52px; line-height: 1.2; }
      .color-field.is-active .field-label { color: var(--uui-color-interactive,#1b264f); font-weight: 600; }

      /* Rich preview */
      .preview-bg { background: #d9d9d9; border-radius: 4px; padding: 4px; }
      .preview-inner { background: #fff; border-radius: 3px; padding: 12px 14px; font-size: 12px; line-height: 1.5; }
      .preview-inner h1,.preview-inner h2,.preview-inner h3,
      .preview-inner h4,.preview-inner h5,.preview-inner h6 { margin: 2px 0; line-height: 1.2; font-weight: 700; }
      .preview-inner h1 { font-size: 22px; }
      .preview-inner h2 { font-size: 19px; }
      .preview-inner h3 { font-size: 16px; }
      .preview-inner h4 { font-size: 14px; }
      .preview-inner h5 { font-size: 13px; }
      .preview-inner h6 { font-size: 12px; }
      .preview-inner hr { margin: 6px 0; border: none; border-top: 1px solid; }
      .preview-inner p { margin: 3px 0; font-size: 12px; }
      .preview-inner a { text-decoration: underline; font-size: 12px; }
      .preview-inner table { width: 100%; border-collapse: collapse; font-size: 11px; margin: 4px 0; }
      .preview-inner td { border: 1px solid; padding: 2px 5px; }
      .preview-btns { display: flex; gap: 5px; flex-wrap: wrap; margin-top: 6px; }
      .preview-btn {
        padding: 3px 10px; border-radius: 3px; border: 1px solid;
        font-size: 11px; font-weight: 500; cursor: default;
      }

      /* Sub-sections */
      .sub-sections { border-top: 1px solid var(--uui-color-border,#d8d7d9); }
      .sub-acc { border-bottom: 1px solid var(--uui-color-border,#d8d7d9); }
      .sub-acc:last-child { border-bottom: none; }
      .sub-header {
        display: flex; align-items: center; gap: 8px; padding: 7px 12px;
        background: var(--uui-color-surface,#fff); cursor: pointer; user-select: none;
      }
      .sub-item.is-open .sub-header { border-bottom: 1px solid var(--uui-color-border,#d8d7d9); }
      .sub-chevron { font-size: 10px; color: var(--uui-color-contrast-subdued,#9ca3af); transition: transform .2s; }
      .sub-item.is-open .sub-chevron { transform: rotate(90deg); }
      .sub-swatch { width: 16px; height: 16px; border-radius: 3px; border: 1px solid rgba(0,0,0,.12); flex-shrink: 0; }
      .sub-label { flex: 1; font-size: 12px; font-weight: 600; color: var(--uui-color-contrast,#1b264f); }
      .sub-body { display: none; }
      .sub-item.is-open .sub-body { display: block; }

      /* Shared picker panel */
      .picker-panel {
        margin: 8px 12px 12px; border: 1px solid var(--uui-color-border,#d8d7d9);
        border-radius: 4px; background: var(--uui-color-surface,#fff); overflow: hidden;
      }
      .picker-bar {
        display: flex; align-items: center; gap: 8px; padding: 6px 10px;
        border-bottom: 1px solid var(--uui-color-border,#d8d7d9);
        background: var(--uui-color-surface-alt,#f6f5f7);
      }
      .picker-swatch { width: 16px; height: 16px; border-radius: 2px; border: 1px solid rgba(0,0,0,.12); flex-shrink: 0; }
      .picker-name { flex: 1; font-size: 11px; font-weight: 600; color: var(--uui-color-contrast,#1b264f); }
      .picker-hex {
        width: 74px; border: 1px solid var(--uui-color-border,#d8d7d9); border-radius: 2px;
        padding: 2px 6px; font-size: 11px; font-family: monospace;
        background: var(--uui-color-surface,#fff);
      }
      .picker-hex:focus { outline: none; border-color: var(--uui-color-interactive,#1b264f); }
      [data-acp] { display: block; min-height: 1px; }

      /* Add button */
      .add-btn {
        display: inline-flex; align-items: center; gap: 6px;
        padding: 6px 14px; border-radius: 4px;
        border: 1px dashed var(--uui-color-border,#d8d7d9);
        background: var(--uui-color-surface-alt,#f6f5f7);
        cursor: pointer; font-size: 12px; color: var(--uui-color-contrast,#1b264f);
      }
      .add-btn:hover { border-color: var(--uui-color-interactive,#1b264f); }
    `,
  ];

  // ── Render helpers ────────────────────────────────────────────────────────

  #colorGrid(itemIdx, section, fields, item) {
    return html`
      <div class="color-grid">
        ${fields.map((f) => {
          const stored = item[section]?.[f.key] ?? "000000";
          const display = toDisplay(stored);
          const isActive = this._active?.itemIdx === itemIdx && this._active?.section === section && this._active?.key === f.key;
          return html`
            <div
              class="color-field ${isActive ? "is-active" : ""}"
              @click=${(/** @type {MouseEvent} */ e) => this.#activateField(itemIdx, section, f.key, f.label, e)}
              title=${f.label}
            >
              <div class="field-swatch" style=${`background:${display}`}></div>
              <span class="field-label">${f.label}</span>
            </div>
          `;
        })}
      </div>
    `;
  }

  #renderPreview(item, showButtons = false) {
    const c = item.content;
    const btn = item.button;
    const sec = item.buttonSecondary;
    const bg = toDisplay(c.background);
    return html`
      <div class="preview-bg">
        <div class="preview-inner" style=${`background:${bg}`}>
          <h1 style=${`color:${toDisplay(c.h1)}`}>H1 Heading</h1>
          <h2 style=${`color:${toDisplay(c.h2)}`}>H2 Heading</h2>
          <h3 style=${`color:${toDisplay(c.h3)}`}>H3 Heading</h3>
          <h4 style=${`color:${toDisplay(c.h4)}`}>H4 Heading</h4>
          <h5 style=${`color:${toDisplay(c.h5)}`}>H5 Heading</h5>
          <h6 style=${`color:${toDisplay(c.h6)}`}>H6 Heading</h6>
          <hr style=${`border-color:${toDisplay(c.border)}`} />
          <p style=${`color:${toDisplay(c.primary)}`}>Body primary — Lorem ipsum futuristic city 2047, <u>Jane discovered a secret facility.</u> <b>Deciphered code unveiled $2 billion!</b></p>
          <p style=${`color:${toDisplay(c.secondary)}`}>Body secondary — <i>revealed a hidden message.</i></p>
          <p><a style=${`color:${toDisplay(c.link)}`} href="#">This is a link in rich text.</a></p>
          <table>
            <tr>
              <td style=${`border-color:${toDisplay(c.border)};color:${toDisplay(c.primary)}`}>Lorem ipsum city</td>
              <td style=${`border-color:${toDisplay(c.border)};color:${toDisplay(c.primary)}`}>Futuristic 2047</td>
            </tr>
          </table>
          ${showButtons ? html`
            <div class="preview-btns">
              <span class="preview-btn" style=${`color:${toDisplay(btn.text)};background:${toDisplay(btn.fill)};border-color:${toDisplay(btn.stroke)}`}>Primary Button</span>
              <span class="preview-btn" style=${`color:${toDisplay(sec.text)};background:transparent;border-color:${toDisplay(sec.stroke)}`}>Secondary Button</span>
            </div>
          ` : null}
        </div>
      </div>
    `;
  }

  #renderButtonPreview(item, sectionKey) {
    const c = item.content;
    const b = item[sectionKey];
    const bg = toDisplay(c.background);
    const fill = sectionKey === "button" ? toDisplay(b.fill) : "transparent";
    return html`
      <div class="preview-bg">
        <div class="preview-inner" style=${`background:${bg}`}>
          <div class="preview-btns">
            <span class="preview-btn" style=${`color:${toDisplay(b.text)};background:${fill};border-color:${toDisplay(b.stroke)}`}>Large Button</span>
            <span class="preview-btn" style=${`color:${toDisplay(b.text)};background:${fill};border-color:${toDisplay(b.stroke)}`}>Normal Button</span>
            <span class="preview-btn" style=${`color:${toDisplay(b.text)};background:${fill};border-color:${toDisplay(b.stroke)};font-size:10px`}>Small Button</span>
          </div>
        </div>
      </div>
    `;
  }

  #renderScheme(item, idx) {
    const isOpen = this._open.has(idx);
    const bgDisplay = toDisplay(item.content.background);
    const activeOnThis = this._active?.itemIdx === idx;
    const activeDisplay = activeOnThis
      ? toDisplay(this._items[idx]?.[this._active.section]?.[this._active.key] ?? "000000")
      : null;

    return html`
      <div class="scheme-item ${isOpen ? "is-open" : ""}">
        <div class="scheme-header" @click=${() => this.#toggleScheme(idx)}>
          <span class="scheme-chevron">▶</span>
          <div class="scheme-swatch" style=${`background:${bgDisplay}`}></div>
          <span class="scheme-title">Style ${idx + 1}</span>
          <div class="scheme-actions" @click=${(/** @type {MouseEvent} */ e) => e.stopPropagation()}>
            <button class="hdr-btn" title="Copy" @click=${() => this.#copyScheme(idx)}>⧉</button>
            <button class="hdr-btn del" title="Delete" @click=${() => this.#deleteScheme(idx)}>✕</button>
          </div>
        </div>

        <div class="scheme-body">
          <!-- Content colors + preview -->
          <div class="content-row">
            <div class="color-panel">${this.#colorGrid(idx, "content", CONTENT_FIELDS, item)}</div>
            <div class="preview-panel">${this.#renderPreview(item, true)}</div>
          </div>

          <!-- Button sub-sections -->
          <div class="sub-sections">
            ${[
              { id: "button", label: "Primary Button", fields: BUTTON_FIELDS },
              { id: "buttonSecondary", label: "Secondary Button", fields: BTN_SEC_FIELDS },
            ].map((sub) => {
              const subKey = `${idx}-${sub.id}`;
              const subOpen = this._secOpen.has(subKey);
              const subItem = item[sub.id];
              const swatchColor = sub.id === "button" ? toDisplay(subItem.fill ?? "000000") : toDisplay(subItem.stroke ?? "000000");
              return html`
                <div class="sub-acc sub-item ${subOpen ? "is-open" : ""}">
                  <div class="sub-header" @click=${() => this.#toggleSection(idx, sub.id)}>
                    <span class="sub-chevron">▶</span>
                    <div class="sub-swatch" style=${`background:${swatchColor}`}></div>
                    <span class="sub-label">${sub.label}</span>
                  </div>
                  <div class="sub-body">
                    <div class="content-row">
                      <div class="color-panel">${this.#colorGrid(idx, sub.id, sub.fields, item)}</div>
                      <div class="preview-panel">${this.#renderButtonPreview(item, sub.id)}</div>
                    </div>
                  </div>
                </div>
              `;
            })}
          </div>

          <!-- Shared picker (shown at bottom of active scheme) -->
          ${activeOnThis && this._active ? html`
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
      <div class="s-sty">
        <div class="scheme-list">
          ${this._items.map((item, idx) => this.#renderScheme(item, idx))}
        </div>
        <button type="button" class="add-btn" @click=${this.#addScheme}>
          <span>+</span> Add Styling
        </button>
      </div>
    `;
  }
}

if (!customElements.get("dcms-styling-property-editor")) {
  customElements.define("dcms-styling-property-editor", DcmsStylingPropertyEditor);
}
