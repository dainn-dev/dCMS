import { LitElement } from "@umbraco-cms/backoffice/external/lit";

const FONTS_LS  = "dcms.fonts.v1";
const STYLING_LS = "dcms.styling.v1";
const FONTS_CH   = "dcms_fonts_v1";
const STYLING_CH = "dcms_styling_v1";

function readLS(key) {
  try { return JSON.parse(localStorage.getItem(key) || "null"); } catch { return null; }
}

export function hexToRgb(hex) {
  const h = String(hex || "").replace(/^#/, "");
  if (h.length !== 6) return "0,0,0";
  const r = parseInt(h.slice(0,2),16), g = parseInt(h.slice(2,4),16), b = parseInt(h.slice(4,6),16);
  return `${r},${g},${b}`;
}

export function headlineTag(levelArr, defaultLevel = "h2") {
  if (Array.isArray(levelArr) && levelArr.length) {
    const l = levelArr[0].toLowerCase();
    if (/^h[1-6]$/.test(l)) return l;
    if (/^[1-6]$/.test(l)) return "h" + l;
  }
  return defaultLevel;
}

export class DcmsBlockBase extends LitElement {
  static properties = {
    content:  { type: Object, attribute: false },
    settings: { type: Object, attribute: false },
    _fonts:   { state: true },
    _schemes: { state: true },
  };

  #fontsChannel   = null;
  #stylingChannel = null;

  constructor() {
    super();
    this.content  = {};
    this.settings = {};
    this._fonts   = [];
    this._schemes = [];
  }

  connectedCallback() {
    super.connectedCallback();
    this._fonts   = readLS(FONTS_LS)   || [];
    this._schemes = readLS(STYLING_LS) || [];
    this._fonts.forEach(f => this.#loadFont(f.fontFamily || f.font?.fontFamily));
    this.#listenFonts();
    this.#listenStyling();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    try { this.#fontsChannel?.close();   } catch {}
    try { this.#stylingChannel?.close(); } catch {}
  }

  #listenFonts() {
    try {
      this.#fontsChannel = new BroadcastChannel(FONTS_CH);
      this.#fontsChannel.onmessage = ({ data }) => {
        if (data?.type === "update" && Array.isArray(data.fonts)) {
          this._fonts = data.fonts;
          data.fonts.forEach(f => this.#loadFont(f.fontFamily || f.font?.fontFamily));
        }
      };
    } catch {}
  }

  #listenStyling() {
    try {
      this.#stylingChannel = new BroadcastChannel(STYLING_CH);
      this.#stylingChannel.onmessage = ({ data }) => {
        if (data?.type === "update" && Array.isArray(data.schemes)) {
          this._schemes = data.schemes;
          this.requestUpdate();
        }
      };
    } catch {}
  }

  #loadFont(family) {
    if (!family) return;
    const href = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(family)}:wght@400;600;700&display=swap`;
    if (!document.querySelector(`link[href="${href}"]`)) {
      document.head.appendChild(Object.assign(document.createElement("link"), { rel:"stylesheet", href }));
    }
  }

  // Resolve scheme by index (1-based) or by name/key string.
  _resolveScheme(bgKey) {
    if (!bgKey || !this._schemes.length) return null;
    const num = parseInt(bgKey, 10);
    if (!isNaN(num) && num >= 1 && num <= this._schemes.length) return this._schemes[num - 1];
    return this._schemes.find(s => s.name === bgKey || s._key === bgKey) || null;
  }

  // Build CSS custom properties string from a scheme.
  _schemeCssVars(scheme) {
    if (!scheme?.content) return "";
    const c = scheme.content, btn = scheme.button || {}, sec = scheme.buttonSecondary || {};
    return `
      background: ${c.background || "#fff"};
      color: ${c.primary || "#333"};
      --block-h1: ${c.h1 || c.primary || "#000"};
      --block-h2: ${c.h2 || c.primary || "#000"};
      --block-h3: ${c.h3 || c.primary || "#000"};
      --block-link: ${c.link || "#2986cc"};
      --block-btn-bg: ${btn.fill || "#2986cc"};
      --block-btn-color: ${btn.text || "#fff"};
      --block-btn-border: ${btn.stroke || "#2986cc"};
    `;
  }

  // Render an image placeholder div.
  _imgPlaceholder(label = "Image") {
    const { html, svg } = this.constructor._litHtmlRefs || {};
    return null; // subclasses use _renderImgPlaceholder
  }
}
