import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const WEBFONTS_URL = "/App_Plugins/DcmsV16/font-picker/webfonts.json";
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
 * @param {string} category
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
    /** text shown in the autocomplete input */
    _inputValue: { state: true },
    _suggestOpen: { state: true },
    _highlightIdx: { state: true },
    _loading: { state: true },
    _toast: { state: true },
    /** @type {Record<string, string | number>} */
    _model: { state: true },
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
    this._inputValue = "";
    this._suggestOpen = false;
    this._highlightIdx = -1;
    this._loading = true;
    this._toast = "";
    this._model = emptyModel();
    this._familyKey = "";
    this._weightOptions = [];
    this._styleOptions = [{ id: "normal", title: "normal" }];
    /** @type {(e: MouseEvent) => void} */
    this._onDocClick = this.#onDocClick.bind(this);
  }

  connectedCallback() {
    super.connectedCallback();
    this.#loadFonts();
    document.addEventListener("click", this._onDocClick, true);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener("click", this._onDocClick, true);
  }

  async #loadFonts() {
    this._loading = true;
    try {
      const res = await fetch(WEBFONTS_URL, { credentials: "same-origin" });
      if (!res.ok) throw new Error(`${res.status}`);
      const data = await res.json();
      this._allFonts = (data.items ?? []).map((font) => ({
        id: font.family,
        family: font.family,
        variants: font.variants ?? [],
        category: font.category ?? "sans-serif",
      }));
    } catch {
      this._allFonts = [];
    } finally {
      this._loading = false;
      this.#syncFromValueProp();
    }
  }

  willUpdate(/** @type {Map<string, unknown>} */ cp) {
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

  #serialize(/** @type {ReturnType<typeof emptyModel>} */ m) {
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
      this._inputValue = "";
      this._weightOptions = [];
      this._styleOptions = [{ id: "normal", title: "normal" }];
      return;
    }
    const saved = this._savedFonts.find((f) => f.id === `${fam}-${w}-${st}`);
    if (saved) {
      this._familyKey = saved.id;
      this._inputValue = `${fam} — ${w} ${st}`;
    } else {
      this._familyKey = fam;
      this._inputValue = fam;
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

  #getSuggestions() {
    const q = this._inputValue.trim().toLowerCase();
    const saved = this._savedFonts.filter(
      (f) => !q || f.fontFamily.toLowerCase().includes(q) || (f.text || "").toLowerCase().includes(q)
    );
    const catalog = this._allFonts
      .filter((f) => !q || f.family.toLowerCase().includes(q))
      .slice(0, q ? 50 : 30);
    return { saved, catalog };
  }

  #onDocClick(/** @type {MouseEvent} */ e) {
    if (!this._suggestOpen) return;
    if (e.composedPath().includes(this)) return;
    this._suggestOpen = false;
  }

  #onInputInput(/** @type {InputEvent} */ e) {
    this._inputValue = /** @type {HTMLInputElement} */ (e.target).value;
    this._suggestOpen = true;
    this._highlightIdx = -1;
    if (!this._inputValue.trim()) {
      this._familyKey = "";
      this._model = { ...emptyModel(), fontSizeDesktop: this._model.fontSizeDesktop, fontSizeTablet: this._model.fontSizeTablet, fontSizeMobile: this._model.fontSizeMobile };
      this.#commit();
    }
  }

  #onInputFocus() {
    this._suggestOpen = true;
    this._highlightIdx = -1;
  }

  #onInputKeydown(/** @type {KeyboardEvent} */ e) {
    if (!this._suggestOpen) {
      if (e.key === "ArrowDown") { this._suggestOpen = true; }
      return;
    }
    const { saved, catalog } = this.#getSuggestions();
    const total = saved.length + catalog.length;
    if (e.key === "ArrowDown") {
      e.preventDefault();
      this._highlightIdx = Math.min(this._highlightIdx + 1, total - 1);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      this._highlightIdx = Math.max(this._highlightIdx - 1, 0);
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (this._highlightIdx >= 0) {
        if (this._highlightIdx < saved.length) {
          this.#onSuggestSelect(saved[this._highlightIdx], true);
        } else {
          this.#onSuggestSelect(catalog[this._highlightIdx - saved.length], false);
        }
      }
    } else if (e.key === "Escape") {
      this._suggestOpen = false;
      this._highlightIdx = -1;
    }
  }

  #onSuggestSelect(/** @type {any} */ item, /** @type {boolean} */ isSaved) {
    this._suggestOpen = false;
    this._highlightIdx = -1;
    if (isSaved) {
      this._familyKey = item.id;
      this._inputValue = `${item.fontFamily} — ${item.fontWeight} ${item.fontStyle}`;
      this._model = {
        ...this._model,
        fontFamily: item.fontFamily,
        fontWeight: item.fontWeight || "400",
        fontStyle: item.fontStyle || "normal",
        fontCategory: item.fontCategory || "",
      };
      this.#applyFontMeta(this._model.fontFamily);
      loadGoogleFont(this._model.fontFamily);
    } else {
      this._familyKey = item.family;
      this._inputValue = item.family;
      this._model = {
        ...emptyModel(),
        fontFamily: item.family,
        fontSizeDesktop: this._model.fontSizeDesktop,
        fontSizeTablet: this._model.fontSizeTablet,
        fontSizeMobile: this._model.fontSizeMobile,
      };
      this.#applyFontMeta(item.family);
      if (this._weightOptions.length) {
        this._model = { ...this._model, fontWeight: this._weightOptions[0].id };
      }
      loadGoogleFont(item.family);
    }
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
    const v = /** @type {HTMLInputElement} */ (e.target)?.value ?? "";
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
    const entry = { id: newId, text: fam, fontFamily: fam, fontWeight: w, fontStyle: st, fontCategory: this._model.fontCategory || "" };
    this._savedFonts = [...this._savedFonts, entry];
    writeSavedFonts(this._savedFonts);
    this._familyKey = newId;
    this._inputValue = `${fam} — ${w} ${st}`;
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
    const fam = saved.fontFamily || saved.text;
    this._familyKey = fam;
    this._inputValue = fam;
    this._model = { ...this._model, fontFamily: fam, fontWeight: saved.fontWeight, fontStyle: saved.fontStyle };
    this.requestUpdate();
  }

  static styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
      }
      .s-font-picker {
        max-width: 321px;
        box-sizing: border-box;
      }
      /* ── Autocomplete ── */
      .autocomplete-wrapper {
        position: relative;
      }
      .font-family-input {
        width: 100%;
        box-sizing: border-box;
        border: solid 1px var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        padding: 6px 8px;
        font: inherit;
        font-size: 13px;
        background: var(--uui-color-surface, #fff);
        color: var(--uui-color-contrast, #1b264f);
      }
      .font-family-input:focus {
        outline: none;
        border-color: var(--uui-color-interactive, #1b264f);
      }
      .suggest-list {
        position: absolute;
        top: 100%;
        left: 0;
        right: 0;
        max-height: 240px;
        overflow-y: auto;
        margin: 2px 0 0;
        padding: 0;
        list-style: none;
        background: var(--uui-color-surface, #fff);
        border: 1px solid var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
        z-index: 9999;
      }
      .suggest-group {
        padding: 4px 10px;
        font-size: 10px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--uui-color-contrast-subdued, #6b7280);
        background: var(--uui-color-surface-alt, #f6f5f7);
        border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
        pointer-events: none;
      }
      .suggest-item {
        padding: 6px 10px;
        cursor: pointer;
        display: flex;
        justify-content: space-between;
        align-items: center;
        font-size: 13px;
        color: var(--uui-color-contrast, #1b264f);
      }
      .suggest-item:hover,
      .suggest-item.highlighted {
        background: var(--uui-color-surface-emphasis, #eef2ff);
      }
      .suggest-meta {
        font-size: 11px;
        color: var(--uui-color-contrast-subdued, #6b7280);
        margin-left: 8px;
        flex-shrink: 0;
      }
      /* ── Weight / Style row ── */
      .s-font-picker .inputs-row select {
        width: 100%;
        box-sizing: border-box;
        background: var(--uui-color-surface, #fff);
        border: solid 1px var(--uui-color-border, #d8d7d9);
        border-radius: 2px;
        min-height: 30px;
        font: inherit;
      }
      .inputs-row {
        display: flex;
        gap: 20px;
        padding-top: 12px;
      }
      .inputs-row.half select {
        flex: 1;
        min-width: 0;
      }
      /* ── Font sizes ── */
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
      .control-label {
        display: block;
        margin: 0 0 4px;
        font-size: 12px;
        font-weight: 600;
        color: var(--uui-color-contrast, #1b264f);
      }
      /* ── Save button area ── */
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
    `,
  ];

  render() {
    if (this._loading) {
      return html`<p style="font-size:13px;color:var(--uui-color-contrast-subdued,#6b7280)">Loading fonts…</p>`;
    }

    const { saved, catalog } = this.#getSuggestions();
    const showSuggest = this._suggestOpen && (saved.length > 0 || catalog.length > 0);
    const currentSaved = this._savedFonts.find((f) => f.id === this._familyKey);
    const notifHidden = !this._toast;

    return html`
      <div class="s-font-picker" data-s-font-picker>

        <!-- Autocomplete input -->
        <div class="autocomplete-wrapper">
          <input
            class="font-family-input"
            type="text"
            placeholder="Type to search fonts…"
            autocomplete="off"
            spellcheck="false"
            .value=${this._inputValue}
            style=${this._model.fontFamily
              ? `font-family: '${this._model.fontFamily}', ${this._model.fontCategory || "sans-serif"};`
              : ""}
            @input=${this.#onInputInput}
            @focus=${this.#onInputFocus}
            @keydown=${this.#onInputKeydown}
          />
          ${showSuggest
            ? html`
                <ul class="suggest-list" role="listbox">
                  ${saved.length
                    ? html`
                        <li class="suggest-group">Saved fonts</li>
                        ${saved.map(
                          (f, i) => html`
                            <li
                              class="suggest-item ${this._highlightIdx === i ? "highlighted" : ""}"
                              role="option"
                              @mousedown=${(/** @type {MouseEvent} */ e) => {
                                e.preventDefault();
                                this.#onSuggestSelect(f, true);
                              }}
                            >
                              <span>${f.fontFamily}</span>
                              <span class="suggest-meta">${f.fontWeight} ${f.fontStyle}</span>
                            </li>
                          `
                        )}
                      `
                    : null}
                  ${catalog.length
                    ? html`
                        <li class="suggest-group">All fonts</li>
                        ${catalog.map(
                          (f, i) => html`
                            <li
                              class="suggest-item ${this._highlightIdx === saved.length + i ? "highlighted" : ""}"
                              role="option"
                              @mousedown=${(/** @type {MouseEvent} */ e) => {
                                e.preventDefault();
                                this.#onSuggestSelect(f, false);
                              }}
                            >
                              ${f.family}
                            </li>
                          `
                        )}
                      `
                    : null}
                </ul>
              `
            : null}
        </div>

        <!-- Weight / Style -->
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

        <!-- Save / Remove -->
        <div class="button-holder">
          <button type="button" class="s-font-picker-btn" @click=${() => this.#saveCurrentFont()}>Save font</button>
          ${currentSaved
            ? html`<button type="button" class="btn-remove-saved" @click=${() => this.#removeCurrentSaved()}>Remove saved</button>`
            : null}
          <span
            class="notification-text ${notifHidden ? "is-hidden" : ""}"
            style=${notifHidden ? "opacity:0;visibility:hidden;pointer-events:none" : "opacity:1;visibility:visible"}
          >
            ${this._toast === "saved"
              ? html`<span class="message-success">
                  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" viewBox="0 0 16 16">
                    <path d="M12 2h-2v3h2z" />
                    <path d="M1.5 0A1.5 1.5 0 0 0 0 1.5v13A1.5 1.5 0 0 0 1.5 16h13a1.5 1.5 0 0 0 1.5-1.5V2.914a1.5 1.5 0 0 0-.44-1.06L14.147.439A1.5 1.5 0 0 0 13.086 0zM4 6a1 1 0 0 1-1-1V1h10v4a1 1 0 0 1-1 1zM3 9h10a1 1 0 0 1 1 1v5H2v-5a1 1 0 0 1 1-1" />
                  </svg>
                  Saved
                </span>`
              : this._toast === "error"
                ? html`<span class="message-error">Font was not saved because it's already saved</span>`
                : null}
          </span>
        </div>

        <!-- Font sizes -->
        <div class="font-sizes-holder">
          <p class="control-label" style="padding-top:15px">Font sizes</p>
          <div class="inputs-row">
            ${[
              { key: "fontSizeDesktop", label: "Desktop" },
              { key: "fontSizeTablet", label: "Tablet" },
              { key: "fontSizeMobile", label: "Mobile" },
            ].map(
              ({ key, label }) => html`
                <div class="input-append">
                  <label class="control-label">${label}</label>
                  <div class="input-holder">
                    <input
                      type="number"
                      min="8"
                      max="54"
                      .value=${String(this._model[key] ?? "")}
                      @change=${(/** @type {Event} */ e) => this.#onSizeChange(/** @type {any} */ (key), e)}
                    />
                    <span class="add-on">px</span>
                  </div>
                </div>
              `
            )}
          </div>
        </div>

      </div>
    `;
  }
}

if (!customElements.get("dcms-font-picker-property-editor")) {
  customElements.define("dcms-font-picker-property-editor", DcmsFontPickerPropertyEditor);
}
