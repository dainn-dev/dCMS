import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const WEBFONTS_URL = "/App_Plugins/UmbracoBlockGrid/GlobalDesign/webfonts.json";
const LS_SAVED_FONTS = "dcms.fontPicker.savedFonts.v1";

function emptyModel() {
  return {
    fontFamily: "",
    fontWeight: "",
    fontStyle: "normal",
    fontCategory: "",
    fontSizeDesktop: "",
    fontSizeTablet: "",
    fontSizeMobile: "",
  };
}

/** @param {string} family */
function loadGoogleFont(family) {
  if (!family) return;
  const key = family.trim().replace(/\s+/g, "+");
  const id = `dcms-gf-${key.replace(/[^a-zA-Z0-9_-]/g, "_")}`;
  if (document.head.querySelector(`#${id}`)) return;
  const link = document.createElement("link");
  link.id = id;
  link.rel = "stylesheet";
  link.href = `https://fonts.googleapis.com/css2?family=${key}:wght@400;700&display=swap`;
  document.head.appendChild(link);
}

/**
 * @param {string[]} variants
 * @returns {{ variants: Record<string, Array<{ id: string, title: string }>>, category: string }}
 */
function parseVariantMap(variants, category) {
  /** @type {Record<string, Array<{ id: string, title: string }>>} */
  const byStyle = {};
  for (const v of variants) {
    const m = String(v).match(/^(\d+)?([a-z]+)?$/i);
    if (!m) continue;
    const weight = m[1] || "400";
    let style = m[2] === "regular" || !m[2] ? "normal" : m[2];
    if (style === "italic" && !m[1]) style = "italic";
    if (!byStyle[style]) byStyle[style] = [];
    if (!byStyle[style].some((x) => x.id === weight)) {
      byStyle[style].push({ id: weight, title: weight });
    }
  }
  for (const k of Object.keys(byStyle)) {
    byStyle[k].sort((a, b) => Number(a.id) - Number(b.id));
  }
  return { variants: byStyle, category: category || "sans-serif" };
}

function readSavedFonts() {
  try {
    const raw = localStorage.getItem(LS_SAVED_FONTS);
    if (!raw) return [];
    const arr = JSON.parse(raw);
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
}

function writeSavedFonts(/** @type {any[]} */ list) {
  try {
    localStorage.setItem(LS_SAVED_FONTS, JSON.stringify(list));
  } catch {
    /* ignore */
  }
}

export default class DcmsFontPickerPropertyEditor extends LitElement {
  static properties = {
    /** @type {string | undefined} */
    value: { type: String },
    _allFonts: { state: true },
    _savedFonts: { state: true },
    _filter: { state: true },
    _loading: { state: true },
    _toast: { state: true },
    /** @type {Record<string, string | number>} */
    _model: { state: true },
    /** current family select value: family name OR saved id */
    _familyKey: { state: true },
    /** @type {Array<{ id: string, title: string }>} */
    _weightOptions: { state: true },
    /** @type {Array<{ id: string, title: string }>} */
    _styleOptions: { state: true },
  };

  constructor() {
    super();
    this.value = undefined;
    this._allFonts = [];
    this._savedFonts = readSavedFonts();
    this._filter = "";
    this._loading = true;
    this._toast = "";
    this._model = emptyModel();
    this._familyKey = "";
    this._weightOptions = [];
    this._styleOptions = [{ id: "normal", title: "normal" }];
  }

  connectedCallback() {
    super.connectedCallback();
    this.#loadFonts();
  }

  async #loadFonts() {
    this._loading = true;
    try {
      const res = await fetch(WEBFONTS_URL, { credentials: "same-origin" });
      if (!res.ok) throw new Error(`${res.status}`);
      const data = await res.json();
      const items = (data.items ?? []).map((font) => ({
        id: font.family,
        family: font.family,
        text: font.family,
        variants: font.variants ?? [],
        category: font.category ?? "sans-serif",
      }));
      this._allFonts = items;
    } catch {
      this._allFonts = [];
    } finally {
      this._loading = false;
      this.#syncFromValueProp();
    }
  }

  willUpdate(cp) {
    if (cp.has("value")) {
      this.#syncFromValueProp();
    }
  }

  #parseStored(/** @type {string | undefined} */ raw) {
    if (raw == null || raw === "") return emptyModel();
    try {
      const o = typeof raw === "string" ? JSON.parse(raw) : raw;
      return { ...emptyModel(), ...o };
    } catch {
      return emptyModel();
    }
  }

  #serialize(/** @type {typeof emptyModel()} */ m) {
    return JSON.stringify({
      fontFamily: m.fontFamily ?? "",
      fontWeight: m.fontWeight ?? "",
      fontStyle: m.fontStyle ?? "normal",
      fontCategory: m.fontCategory ?? "",
      fontSizeDesktop: m.fontSizeDesktop ?? "",
      fontSizeTablet: m.fontSizeTablet ?? "",
      fontSizeMobile: m.fontSizeMobile ?? "",
    });
  }

  #syncFromValueProp() {
    const m = this.#parseStored(this.value);
    this._model = { ...m };
    const fam = m.fontFamily || "";
    const w = m.fontWeight || "";
    const st = m.fontStyle || "normal";
    if (!fam) {
      this._familyKey = "";
      this._weightOptions = [];
      this._styleOptions = [{ id: "normal", title: "normal" }];
      return;
    }
    const saved = this._savedFonts.find((f) => f.id === `${fam}-${w}-${st}`);
    if (saved) {
      this._familyKey = saved.id;
    } else {
      this._familyKey = fam;
    }
    this.#applyFontMeta(fam);
    loadGoogleFont(fam);
  }

  /** @param {string} familyName */
  #applyFontMeta(familyName) {
    const font = this._allFonts.find((f) => f.family === familyName);
    if (!font) {
      this._weightOptions = this._model.fontWeight ? [{ id: String(this._model.fontWeight), title: String(this._model.fontWeight) }] : [];
      this._styleOptions = [{ id: String(this._model.fontStyle || "normal"), title: String(this._model.fontStyle || "normal") }];
      return;
    }
    const { variants, category } = parseVariantMap(font.variants, font.category);
    this._model = { ...this._model, fontCategory: category };
    const styleKeys = Object.keys(variants);
    this._styleOptions = styleKeys.map((k) => ({ id: k, title: k }));
    let st = this._model.fontStyle || "normal";
    if (!variants[st] && styleKeys.length) {
      st = styleKeys[0];
      this._model = { ...this._model, fontStyle: st };
    }
    this._weightOptions = variants[st] || [];
    let w = this._model.fontWeight || "";
    if (this._weightOptions.length && !this._weightOptions.some((x) => x.id === w)) {
      w = this._weightOptions[0].id;
      this._model = { ...this._model, fontWeight: w };
    }
  }

  #commit() {
    const next = this.#serialize(this._model);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  #filteredCatalog() {
    const q = this._filter.trim().toLowerCase();
    if (!q) return this._allFonts.slice(0, 200);
    return this._allFonts.filter((f) => f.family.toLowerCase().includes(q)).slice(0, 200);
  }

  #onFamilyChange(/** @type {Event} */ e) {
    const sel = /** @type {HTMLSelectElement} */ (e.target);
    const v = sel.value;
    if (!v) {
      this._familyKey = "";
      this._model = emptyModel();
      this.#commit();
      return;
    }
    const saved = this._savedFonts.find((f) => f.id === v);
    if (saved) {
      this._familyKey = saved.id;
      this._model = {
        ...this._model,
        fontFamily: saved.text || saved.fontFamily,
        fontWeight: saved.fontWeight || "400",
        fontStyle: saved.fontStyle || "normal",
        fontCategory: saved.fontCategory || "",
      };
      this.#applyFontMeta(this._model.fontFamily);
      loadGoogleFont(this._model.fontFamily);
      this.#commit();
      return;
    }
    this._familyKey = v;
    this._model = {
      ...emptyModel(),
      fontFamily: v,
      fontWeight: "",
      fontStyle: "normal",
      fontSizeDesktop: this._model.fontSizeDesktop,
      fontSizeTablet: this._model.fontSizeTablet,
      fontSizeMobile: this._model.fontSizeMobile,
    };
    this.#applyFontMeta(v);
    if (this._weightOptions.length) {
      this._model = { ...this._model, fontWeight: this._weightOptions[0].id };
    }
    loadGoogleFont(v);
    this.#commit();
  }

  #onWeightChange(/** @type {Event} */ e) {
    const v = /** @type {HTMLSelectElement} */ (e.target).value;
    this._model = { ...this._model, fontWeight: v };
    this.#commit();
  }

  #onStyleChange(/** @type {Event} */ e) {
    const v = /** @type {HTMLSelectElement} */ (e.target).value;
    this._model = { ...this._model, fontStyle: v };
    this.#applyFontMeta(this._model.fontFamily);
    if (this._weightOptions.length) {
      this._model = { ...this._model, fontWeight: this._weightOptions[0].id };
    }
    this.#commit();
  }

  #onSizeChange(/** @type {keyof ReturnType<typeof emptyModel>} */ key, /** @type {Event} */ e) {
    const el = /** @type {HTMLInputElement} */ (e.target);
    const v = el?.value ?? "";
    this._model = { ...this._model, [key]: v };
    this.#commit();
  }

  #saveCurrentFont() {
    const fam = this._model.fontFamily;
    if (!fam) return;
    const w = this._model.fontWeight || "400";
    const st = this._model.fontStyle || "normal";
    const newId = `${fam}-${w}-${st}`;
    if (this._savedFonts.some((f) => f.id === newId)) {
      this._toast = "error";
      setTimeout(() => (this._toast = ""), 2000);
      return;
    }
    const cat = this._model.fontCategory || "";
    const entry = {
      id: newId,
      text: fam,
      fontFamily: fam,
      fontWeight: w,
      fontStyle: st,
      fontCategory: cat,
    };
    this._savedFonts = [...this._savedFonts, entry];
    writeSavedFonts(this._savedFonts);
    this._familyKey = newId;
    this._toast = "saved";
    setTimeout(() => (this._toast = ""), 2000);
    this.requestUpdate();
  }

  #removeCurrentSaved() {
    const id = this._familyKey;
    const saved = this._savedFonts.find((f) => f.id === id);
    if (!saved) return;
    this._savedFonts = this._savedFonts.filter((f) => f.id !== id);
    writeSavedFonts(this._savedFonts);
    this._familyKey = saved.fontFamily || saved.text;
    this._model = {
      ...this._model,
      fontFamily: saved.fontFamily || saved.text,
      fontWeight: saved.fontWeight,
      fontStyle: saved.fontStyle,
    };
    this.requestUpdate();
  }

  static styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }
      /* Mirrors UmbracoBlockGrid/FontPicker/css/fontPicker.css layout */
      .s-font-picker {
        max-width: 321px;
        box-sizing: border-box;
      }
      .s-font-picker select.font-family-select,
      .s-font-picker .inputs-row select {
        width: 100%;
        box-sizing: border-box;
        background: var(--uui-color-surface, #fff);
        border: solid 1px var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        min-height: 30px;
        font: inherit;
      }
      .filter-row {
        margin-top: 15px;
        margin-bottom: 10px;
      }
      .filter-row:first-child {
        margin-top: 0;
      }
      .control-label {
        width: auto;
        display: block;
        float: unset;
        margin: 0 0 4px;
        font-size: 12px;
        font-weight: 600;
        color: var(--uui-color-contrast, #1b264f);
      }
      .inputs-row {
        display: flex;
        gap: 20px;
        padding-top: 20px;
      }
      .inputs-row.half select {
        flex: 1;
        min-width: 0;
      }
      .font-sizes-holder {
        display: flex;
        flex-direction: column;
      }
      .font-sizes-holder > p.control-label {
        margin-bottom: 0;
        padding-top: 15px;
      }
      .font-sizes-holder .inputs-row {
        padding-top: 0;
      }
      .input-append {
        display: flex;
        flex-direction: column;
        width: 33.33333333%;
        min-width: 0;
      }
      .input-holder {
        display: flex;
        align-items: stretch;
      }
      .input-holder input[type="number"] {
        flex: 1;
        min-width: 0;
        box-sizing: border-box;
        border: solid 1px var(--uui-color-border, #d8d7d9);
        border-right: none;
        border-radius: 2px 0 0 2px;
        padding: 4px 8px;
        font: inherit;
        background: var(--uui-color-surface, #fff);
      }
      .input-holder .add-on {
        display: inline-flex;
        align-items: center;
        padding: 0 8px;
        border: solid 1px var(--uui-color-border, #d8d7d9);
        border-radius: 0 2px 2px 0;
        background: var(--uui-color-surface-alt, #f6f5f7);
        font-size: 12px;
        color: var(--uui-color-contrast, #1b264f);
        white-space: nowrap;
      }
      .button-holder {
        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: 8px;
        padding-top: 10px;
        min-height: 44px;
      }
      .s-font-picker-btn {
        flex-shrink: 0;
        padding: 3px 10px;
        font-family: var(--uui-font-family, "Lato", "Helvetica Neue", Helvetica, Arial, sans-serif);
        font-weight: 500;
        font-size: 12px;
        border: none;
        border-radius: 4px;
        cursor: pointer;
        color: #fff;
        background: #000;
      }
      .s-font-picker-btn:hover {
        background: #222;
      }
      .btn-remove-saved {
        flex-shrink: 0;
        padding: 3px 8px;
        font-size: 11px;
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 4px;
        cursor: pointer;
        background: var(--uui-color-surface, #fff);
        color: var(--uui-color-contrast, #1b264f);
      }
      .notification-text {
        display: inline-flex;
        align-items: center;
        padding-left: 10px;
        font-size: 13px;
        line-height: 1.2;
        transition: opacity 0.3s;
      }
      .notification-text.is-hidden {
        opacity: 0;
        visibility: hidden;
        pointer-events: none;
      }
      .notification-text .message-success {
        display: inline-flex;
        align-items: center;
        color: green;
      }
      .notification-text .message-error {
        display: inline-flex;
        color: #df740e;
      }
      .notification-text svg {
        margin-right: 5px;
      }
      .filter-input {
        width: 100%;
        box-sizing: border-box;
        border: solid 1px var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        padding: 6px 8px;
        font: inherit;
        background: var(--uui-color-surface, #fff);
      }
    `,
  ];

  render() {
    if (this._loading) {
      return html`<p>Loading fonts…</p>`;
    }
    const catalog = this.#filteredCatalog();
    const currentSaved = this._savedFonts.find((f) => f.id === this._familyKey);
    const notifHidden = !this._toast;
    return html`
      <div class="s-font-picker" data-s-font-picker>
        <div class="filter-row">
          <label class="control-label">Search fonts</label>
          <input
            class="filter-input"
            type="search"
            placeholder="Filter list (max 200)…"
            autocomplete="off"
            .value=${this._filter}
            @input=${(/** @type {InputEvent} */ e) => {
              this._filter = /** @type {HTMLInputElement} */ (e.target).value;
            }}
          />
        </div>

        <select
          class="font-family-select"
          data-font-picker
          .value=${this._familyKey}
          style=${`font-family: '${this._model.fontFamily || "inherit"}', ${this._model.fontCategory || "sans-serif"};`}
          @change=${this.#onFamilyChange}
        >
          <option value="">—</option>
          ${this._savedFonts.length
            ? html`<optgroup label="Saved fonts">
                ${this._savedFonts.map(
                  (f) => html`<option value=${f.id}>${f.text} — ${f.fontWeight} ${f.fontStyle}</option>`
                )}
              </optgroup>`
            : null}
          <optgroup label="All fonts (filtered)">
            ${catalog.map((f) => html`<option value=${f.id}>${f.family}</option>`)}
          </optgroup>
        </select>

        <div class="inputs-row half">
          <select data-font-picker-weight .value=${String(this._model.fontWeight ?? "")} @change=${this.#onWeightChange}>
            ${this._weightOptions.length
              ? this._weightOptions.map((o) => html`<option value=${o.id}>${o.title}</option>`)
              : this._model.fontWeight
                ? html`<option value=${String(this._model.fontWeight)}>${this._model.fontWeight}</option>`
                : html`<option value="">—</option>`}
          </select>
          <select data-font-picker-style .value=${String(this._model.fontStyle ?? "normal")} @change=${this.#onStyleChange}>
            ${this._styleOptions.length
              ? this._styleOptions.map((o) => html`<option value=${o.id}>${o.title}</option>`)
              : html`<option value=${String(this._model.fontStyle || "normal")}>${this._model.fontStyle || "normal"}</option>`}
          </select>
        </div>

        <div class="button-holder">
          <button type="button" class="s-font-picker-btn" @click=${() => this.#saveCurrentFont()}>Save font</button>
          ${currentSaved
            ? html`<button type="button" class="btn-remove-saved" @click=${() => this.#removeCurrentSaved()}>Remove saved</button>`
            : null}
          <span
            class="notification-text ${notifHidden ? "is-hidden" : ""}"
            data-notification
            style=${notifHidden ? "opacity:0;visibility:hidden;pointer-events:none" : "opacity:1;visibility:visible"}
          >
            ${this._toast === "saved"
              ? html`<span class="message-success"
                  ><svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                    <path d="M12 2h-2v3h2z" />
                    <path
                      d="M1.5 0A1.5 1.5 0 0 0 0 1.5v13A1.5 1.5 0 0 0 1.5 16h13a1.5 1.5 0 0 0 1.5-1.5V2.914a1.5 1.5 0 0 0-.44-1.06L14.147.439A1.5 1.5 0 0 0 13.086 0zM4 6a1 1 0 0 1-1-1V1h10v4a1 1 0 0 1-1 1zM3 9h10a1 1 0 0 1 1 1v5H2v-5a1 1 0 0 1 1-1"
                    />
                  </svg></span>`
              : this._toast === "error"
                ? html`<span class="message-error">Font was not saved because it’s already saved</span>`
                : null}
          </span>
        </div>

        <div class="font-sizes-holder">
          <p class="control-label">Font sizes</p>
          <div class="inputs-row">
            <div class="input-append">
              <label class="control-label">Desktop</label>
              <div class="input-holder">
                <input
                  type="number"
                  min="8"
                  max="54"
                  .value=${String(this._model.fontSizeDesktop ?? "")}
                  @change=${(e) => this.#onSizeChange("fontSizeDesktop", e)}
                />
                <span class="add-on">px</span>
              </div>
            </div>
            <div class="input-append">
              <label class="control-label">Tablet</label>
              <div class="input-holder">
                <input
                  type="number"
                  min="8"
                  max="54"
                  .value=${String(this._model.fontSizeTablet ?? "")}
                  @change=${(e) => this.#onSizeChange("fontSizeTablet", e)}
                />
                <span class="add-on">px</span>
              </div>
            </div>
            <div class="input-append">
              <label class="control-label">Mobile</label>
              <div class="input-holder">
                <input
                  type="number"
                  min="8"
                  max="54"
                  .value=${String(this._model.fontSizeMobile ?? "")}
                  @change=${(e) => this.#onSizeChange("fontSizeMobile", e)}
                />
                <span class="add-on">px</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  }
}

if (!customElements.get("dcms-font-picker-property-editor")) {
  customElements.define("dcms-font-picker-property-editor", DcmsFontPickerPropertyEditor);
}
