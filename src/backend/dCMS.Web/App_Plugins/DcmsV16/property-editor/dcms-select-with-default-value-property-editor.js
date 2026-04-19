import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

/**
 * Items config format (from settings.properties MultipleTextString):
 *   JSON array of strings, each string is "Label|value" or just "value".
 *   Falls back to comma-separated plain string for legacy compat.
 */
function parseItems(raw) {
  if (!raw) return [];
  try {
    const arr = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (Array.isArray(arr)) {
      return arr.map(item => {
        if (typeof item === "object" && item !== null) {
          return { label: String(item.value ?? item.label ?? item.id ?? ""), value: String(item.id ?? item.value ?? "") };
        }
        const s = String(item);
        const pipe = s.indexOf("|");
        if (pipe !== -1) return { label: s.slice(0, pipe).trim(), value: s.slice(pipe + 1).trim() };
        return { label: s.trim(), value: s.trim() };
      });
    }
  } catch { /* fall through */ }
  return String(raw).split(",").map(s => s.trim()).filter(Boolean).map(s => {
    const pipe = s.indexOf("|");
    if (pipe !== -1) return { label: s.slice(0, pipe).trim(), value: s.slice(pipe + 1).trim() };
    return { label: s, value: s };
  });
}

export default class DcmsSelectWithDefaultValuePropertyEditor extends LitElement {
  static properties = {
    value:  { type: String },
    config: { type: Object },
  };

  constructor() {
    super();
    this.value  = "";
    this.config = {};
  }

  get #items() {
    return parseItems(this.config?.items);
  }

  get #defaultValue() {
    return this.config?.defaultValue ?? "";
  }

  connectedCallback() {
    super.connectedCallback();
    if (!this.value) {
      const def = this.#defaultValue;
      if (def) this.#emit(def);
    }
  }

  willUpdate(changed) {
    if (changed.has("value") && !this.value) {
      const def = this.#defaultValue;
      if (def) this.#emit(def);
    }
  }

  #emit(val) {
    if (val !== this.value) {
      this.value = val;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  #onChange(e) {
    this.#emit(e.target.value);
  }

  render() {
    const items   = this.#items;
    const current = this.value || this.#defaultValue;
    if (items.length === 0) {
      return html`<p class="empty">No items configured. Add options via data type settings (Items).</p>`;
    }
    return html`
      <div class="wrap">
        <select class="sel" .value=${current} @change=${this.#onChange}>
          ${items.map(item => html`
            <option value=${item.value} ?selected=${item.value === current}>${item.label}</option>
          `)}
        </select>
        ${current ? html`<span class="hint">Selected: <strong>${current}</strong></span>` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-direction: column; gap: 6px; }
    .sel {
      width: 100%; padding: 7px 10px; font-size: 14px;
      border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 4px;
      background: #fff; cursor: pointer;
    }
    .sel:focus { outline: none; border-color: var(--uui-color-current, #3544b1); }
    .hint { font-size: 12px; color: var(--uui-color-text-alt, #767676); }
    .empty { color: var(--uui-color-text-alt, #767676); font-size: 13px; margin: 0; font-style: italic; }
  `];
}

customElements.define("dcms-select-with-default-value-property-editor", DcmsSelectWithDefaultValuePropertyEditor);
