import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const STOP_WORDS = new Set([
  "here","there","where","inside","outside","above","below","i","now","when","then",
  "today","yesterday","tomorrow","before","lately","recently","once","ever","never",
  "always","seldom","usually","sometimes","already","yet","still","since","much",
  "little","very","too","so","enough","hardly","scarcely","nearly","almost","well",
  "fast","quickly","slowly","quietly","easily","also","either","else","only","how",
  "you","he","she","it","we","they","me","him","her","us","them","my","mine","your",
  "yours","his","hers","its","our","ours","their","theirs","this","that","these",
  "those","such","same","myself","yourself","himself","herself","itself","ourselves",
  "yourselves","themselves","who","what","which","whose","whoever","whatever",
  "whichever","some","something","somebody","someone","any","anything","anybody",
  "anyone","no","nothing","nobody","none","neither","other","another","all","each",
  "both","either","every","everything","everybody","everyone","whom","why","again",
  "in","on","at","to","for","with","about","against","between","among","from","into",
  "through","and","or","but","because","although","if","while","though","am","is",
  "are","was","were","be","been","being","do","does","did","have","has","had","having",
  "a","an","the","of","as","by","can","could","will","would","shall","should","must",
  "may","might","different","various","certain","several","many","most","more","few",
  "fewer","least","best","worst","whole","entire","own","really","almost","currently",
  "soon","article","blog","post","page","section","chapter","part","topic","heading",
  "subheading","introduction","conclusion","summary","one","than","pretty",
  "accurately","badly","carefully","hard","straight","politely","near","ahead",
  "early","extremely","quite","frequently","definitely","clearly","surely","perhaps",
  "obviously","certainly","probably","maybe"
]);

const EDITOR_SELECTOR = '[data-element="editor-container"]';
const SEO_ATTR = "[seo-text-counter]";

function scanContent(root) {
  let totalChars = 0;
  let totalWords = 0;
  const freqMap = new Map();

  function traverse(node) {
    if (!node) return;
    if (node.nodeType === Node.ELEMENT_NODE) {
      if (node.matches?.(SEO_ATTR)) {
        const text = node.textContent.trim();
        if (text && text !== "\u00a0") {
          totalChars += text.length;
          const words = text.match(/[a-zA-Z0-9_'']+/g) || [];
          totalWords += words.length;
          for (const w of words) {
            const key = w.toLowerCase().replace(/[_'']/g, "");
            if (!STOP_WORDS.has(key)) {
              const entry = freqMap.get(key) ?? { original: w, count: 0 };
              entry.count++;
              freqMap.set(key, entry);
            }
          }
        }
      }
      if (node.shadowRoot) traverse(node.shadowRoot);
    }
    if (node.childNodes) node.childNodes.forEach(traverse);
  }

  traverse(root);

  const topWords = [...freqMap.values()]
    .sort((a, b) => b.count - a.count)
    .slice(0, 20);

  return { chars: totalChars, words: totalWords, topWords };
}

function wordStatus(words) {
  if (words < 1500)  return "warn";
  if (words <= 3000) return "good";
  return "warn";
}

function wordMsg(words) {
  if (words < 1500)  return "Content has fewer than 1500 words. Consider adding more detailed content for better SEO.";
  if (words <= 3000) return "Great! Content length is optimal for SEO and user engagement.";
  return "Content exceeds 3000 words. Consider splitting into smaller focused pages.";
}

export default class DcmsSeoContentPropertyEditor extends LitElement {
  static properties = {
    _result:  { state: true },
    _loading: { state: true },
  };

  #observer = null;

  constructor() {
    super();
    this._result  = null;
    this._loading = false;
  }

  connectedCallback() {
    super.connectedCallback();
    this.#scan();
    this.#observe();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#observer?.disconnect();
    this.#observer = null;
  }

  #scan() {
    this._loading = true;
    setTimeout(() => {
      const container = document.querySelector(EDITOR_SELECTOR);
      this._result  = container ? scanContent(container) : { chars: 0, words: 0, topWords: [] };
      this._loading = false;
    }, 100);
  }

  #observe() {
    const container = document.querySelector(EDITOR_SELECTOR);
    if (!container) return;
    this.#observer = new MutationObserver(() => this.#scan());
    this.#observer.observe(container, { childList: true, subtree: true, characterData: true });
  }

  render() {
    const r  = this._result;
    const st = r ? wordStatus(r.words) : "empty";
    return html`
      <div class="wrap">
        <div class="toolbar">
          <span class="title">Content Analysis</span>
          <button class="refresh-btn" @click=${() => this.#scan()}>↻ Refresh</button>
        </div>

        ${this._loading ? html`<p class="loading">Scanning…</p>` : r ? html`

          <div class="stats">
            <div class="stat">
              <span class="stat-val">${r.words.toLocaleString()}</span>
              <span class="stat-lbl">Words</span>
            </div>
            <div class="stat">
              <span class="stat-val">${r.chars.toLocaleString()}</span>
              <span class="stat-lbl">Characters</span>
            </div>
          </div>

          <div class="rec rec--${st}">
            <span class="rec-icon">${st === "good" ? "✓" : "⚠"}</span>
            <span>${wordMsg(r.words)}</span>
          </div>

          ${r.topWords.length > 0 ? html`
            <div class="kw-section">
              <div class="kw-title">Top Keywords</div>
              <div class="kw-list">
                ${r.topWords.map((entry, i) => html`
                  <div class="kw-row">
                    <span class="kw-rank">${i + 1}</span>
                    <span class="kw-word">${entry.original}</span>
                    <div class="kw-bar-wrap">
                      <div class="kw-bar" style="width:${Math.round((entry.count / r.topWords[0].count) * 100)}%"></div>
                    </div>
                    <span class="kw-count">${entry.count}</span>
                  </div>
                `)}
              </div>
            </div>
          ` : html`<p class="empty">No significant keywords found.</p>`}

        ` : html`<p class="empty">No content found. Make sure content elements have the <code>seo-text-counter</code> attribute.</p>`}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-direction: column; gap: 10px; }
    .toolbar {
      display: flex; align-items: center; gap: 8px;
      padding-bottom: 8px; border-bottom: 1px solid var(--uui-color-border, #e3e3e3);
    }
    .title { font-size: 13px; font-weight: 600; flex: 1; }
    .refresh-btn {
      padding: 3px 10px; font-size: 12px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; background: #fff; cursor: pointer;
    }
    .refresh-btn:hover { background: var(--uui-color-surface-alt, #f5f5f5); }
    .loading, .empty { color: var(--uui-color-text-alt, #767676); font-size: 13px; margin: 0; }
    .stats { display: flex; gap: 12px; }
    .stat {
      flex: 1; display: flex; flex-direction: column; align-items: center; padding: 12px;
      border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 6px;
      background: var(--uui-color-surface-alt, #f5f5f5);
    }
    .stat-val { font-size: 22px; font-weight: 700; }
    .stat-lbl { font-size: 11px; color: var(--uui-color-text-alt, #767676); }
    .rec {
      display: flex; align-items: flex-start; gap: 8px; padding: 10px 12px;
      border-radius: 6px; font-size: 13px;
    }
    .rec--good { background: #f0fdf4; color: #15803d; border: 1px solid #bbf7d0; }
    .rec--warn { background: #fffbeb; color: #b45309; border: 1px solid #fde68a; }
    .rec--empty { background: var(--uui-color-surface-alt, #f5f5f5); color: var(--uui-color-text-alt, #767676); }
    .rec-icon { font-weight: 700; flex-shrink: 0; }
    .kw-section { border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 6px; overflow: hidden; }
    .kw-title {
      padding: 8px 12px; font-size: 12px; font-weight: 600;
      background: var(--uui-color-surface-alt, #f5f5f5);
      border-bottom: 1px solid var(--uui-color-border, #e3e3e3);
    }
    .kw-list { display: flex; flex-direction: column; }
    .kw-row {
      display: flex; align-items: center; gap: 8px; padding: 6px 12px; font-size: 13px;
    }
    .kw-row + .kw-row { border-top: 1px solid var(--uui-color-border, #e3e3e3); }
    .kw-rank { width: 20px; color: var(--uui-color-text-alt, #767676); font-size: 11px; text-align: right; flex-shrink: 0; }
    .kw-word { width: 120px; flex-shrink: 0; font-weight: 500; }
    .kw-bar-wrap { flex: 1; height: 6px; background: #e5e7eb; border-radius: 3px; overflow: hidden; }
    .kw-bar { height: 100%; background: var(--uui-color-current, #3544b1); border-radius: 3px; }
    .kw-count { width: 30px; text-align: right; color: var(--uui-color-text-alt, #767676); font-size: 12px; flex-shrink: 0; }
    code { font-size: 11px; background: #e5e7eb; padding: 1px 4px; border-radius: 3px; }
  `];
}

customElements.define("dcms-seo-content-property-editor", DcmsSeoContentPropertyEditor);
