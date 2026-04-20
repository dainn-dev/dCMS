import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const STYLING_CHANNEL = "dcms_styling_v1";
const STYLING_LS_KEY  = "dcms.styling.v1";

function loadSchemes() {
  try {
    const raw = localStorage.getItem(STYLING_LS_KEY);
    if (raw) {
      const arr = JSON.parse(raw);
      if (Array.isArray(arr) && arr.length) return arr;
    }
  } catch { /* fall through */ }
  return [];
}

function parseValue(raw) {
  try {
    const v = typeof raw === "string" ? JSON.parse(raw) : raw;
    if (v && typeof v === "object" && v.content) return v;
  } catch { /* fall through */ }
  return null;
}

function schemeIndex(schemes, val) {
  if (!val || !schemes.length) return -1;
  return schemes.findIndex(s =>
    s.content?.background === val.content?.background &&
    s.button?.fill === val.button?.fill
  );
}

export default class DcmsDesignStylingPropertyEditor extends LitElement {
  static properties = {
    value:    { type: String },
    _schemes: { state: true },
    _selIdx:  { state: true },
  };

  #channel = null;

  constructor() {
    super();
    this.value    = undefined;
    this._schemes = [];
    this._selIdx  = -1;
  }

  connectedCallback() {
    super.connectedCallback();
    this._schemes = loadSchemes();
    this._selIdx  = schemeIndex(this._schemes, parseValue(this.value));
    this.#listenChannel();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    try { this.#channel?.close(); } catch { }
    this.#channel = null;
  }

  willUpdate(changed) {
    if (changed.has("value")) {
      this._selIdx = schemeIndex(this._schemes, parseValue(this.value));
    }
  }

  #listenChannel() {
    try {
      this.#channel = new BroadcastChannel(STYLING_CHANNEL);
      this.#channel.onmessage = ({ data }) => {
        if (data?.type === "update" && Array.isArray(data.schemes)) {
          this._schemes = data.schemes;
          this._selIdx  = schemeIndex(this._schemes, parseValue(this.value));
        }
      };
    } catch { /* BroadcastChannel not supported */ }
  }

  #select(idx) {
    this._selIdx = idx;
    const scheme = this._schemes[idx];
    const next = JSON.stringify(scheme);
    if (next !== this.value) {
      this.value = next;
      this.dispatchEvent(new UmbChangeEvent());
    }
  }

  render() {
    if (this._schemes.length === 0) {
      return html`
        <p class="empty">
          No color schemes available. Add schemes using the <strong>Dainn Styling Editor</strong> property first.
        </p>
      `;
    }
    return html`
      <div class="grid">
        ${this._schemes.map((s, i) => this.#renderCard(s, i))}
      </div>
    `;
  }

  #renderCard(s, i) {
    const active = i === this._selIdx;
    const bg     = s.content?.background ?? "#fff";
    const btnFill = s.button?.fill ?? "#2986cc";
    const btnTxt  = s.button?.text ?? "#fff";
    const btnStroke = s.button?.stroke ?? "#2986cc";
    const secTxt  = s.buttonSecondary?.text ?? "#2986cc";
    const secStroke = s.buttonSecondary?.stroke ?? "#2986cc";
    const primary   = s.content?.primary ?? "#5b5b5b";

    return html`
      <div class="card ${active ? "active" : ""}" @click=${() => this.#select(i)}>
        <div class="card-inner" style="background:${bg}">
          <div class="card-heads">
            ${["h1","h2","h3"].map(h => html`
              <span class="card-h" style="color:${s.content?.[h] ?? "#000"}">${h}</span>
            `)}
          </div>
          <div class="card-text" style="color:${primary}">text</div>
          <div class="card-btns">
            <div class="card-btn" style="color:${btnTxt};background:${btnFill};border-color:${btnStroke}">Primary</div>
            <div class="card-btn card-btn--sec" style="color:${secTxt};border-color:${secStroke}">Secondary</div>
          </div>
        </div>
        <div class="card-footer">
          <span class="card-num">Style ${i + 1}</span>
          ${active ? html`<span class="card-check">✓</span>` : ""}
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .empty { font-size: 13px; color: var(--uui-color-text-alt, #767676); font-style: italic; margin: 0; }
    .grid { display: flex; flex-wrap: wrap; gap: 10px; }
    .card {
      width: 140px; border: 2px solid var(--uui-color-border, #e3e3e3);
      border-radius: 6px; overflow: hidden; cursor: pointer;
      transition: border-color .15s, box-shadow .15s;
    }
    .card:hover { border-color: var(--uui-color-current-emphasis, #5a67e8); }
    .card.active {
      border-color: var(--uui-color-current, #3544b1);
      box-shadow: 0 0 0 2px rgba(53,68,177,.2);
    }
    .card-inner { padding: 8px; font-size: 10px; }
    .card-heads { display: flex; gap: 4px; margin-bottom: 3px; }
    .card-h { font-weight: 700; }
    .card-text { margin-bottom: 4px; }
    .card-btns { display: flex; flex-direction: column; gap: 3px; }
    .card-btn {
      padding: 2px 5px; border: 1px solid; border-radius: 2px;
      font-size: 9px; text-align: center; line-height: 1.4;
    }
    .card-btn--sec { background: transparent; }
    .card-footer {
      display: flex; justify-content: space-between; align-items: center;
      padding: 4px 8px; font-size: 11px; font-weight: 600;
      background: var(--uui-color-surface-alt, #f5f5f5);
      border-top: 1px solid var(--uui-color-border, #e3e3e3);
    }
    .card-check { color: var(--uui-color-current, #3544b1); font-weight: 700; }
  `];
}

customElements.define("dcms-design-styling-property-editor", DcmsDesignStylingPropertyEditor);
