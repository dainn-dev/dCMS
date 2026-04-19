import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const LEVELS = ["h1", "h2", "h3", "h4", "h5", "h6"];

function collectHeadings(root) {
  const results = [];
  function walk(node) {
    if (!node) return;
    if (node.nodeType === Node.ELEMENT_NODE) {
      const tag = node.tagName.toLowerCase();
      if (LEVELS.includes(tag)) {
        results.push({ level: parseInt(tag[1]), text: (node.textContent || "").trim() });
      }
      if (node.shadowRoot) walk(node.shadowRoot);
      for (const child of node.children) walk(child);
    } else if (node.nodeType === Node.DOCUMENT_FRAGMENT_NODE) {
      for (const child of node.children) walk(child);
    }
  }
  walk(root);
  return results;
}

function buildTree(headings) {
  const root = { children: [] };
  const stack = [root];
  for (const h of headings) {
    const node = { level: h.level, text: h.text, children: [] };
    while (stack.length > 1 && stack[stack.length - 1].level >= h.level) stack.pop();
    stack[stack.length - 1].children.push(node);
    stack.push(node);
  }
  return root.children;
}

function analyzeHeadings(headings) {
  const issues = [];
  const h1s = headings.filter(h => h.level === 1);
  if (h1s.length === 0)  issues.push({ type: "warn", msg: "No H1 found. Every page should have exactly one H1." });
  if (h1s.length > 1)    issues.push({ type: "warn", msg: `Multiple H1s found (${h1s.length}). There should be only one H1.` });

  for (let i = 1; i < headings.length; i++) {
    const prev = headings[i - 1];
    const cur  = headings[i];
    if (cur.level > prev.level + 1) {
      issues.push({ type: "warn", msg: `Non-sequential heading: H${prev.level} → H${cur.level} (skipped levels).` });
    }
  }

  const h2s = headings.filter(h => h.level === 2);
  const h3s = headings.filter(h => h.level === 3);
  if (h2s.length > 0 && h3s.length === 0) {
    issues.push({ type: "info", msg: "H2 headings present but no H3 subheadings — consider adding structure." });
  }

  if (issues.length === 0) {
    issues.push({ type: "good", msg: "Heading structure looks good!" });
  }
  return issues;
}

export default class DcmsSeoHeadersPropertyEditor extends LitElement {
  static properties = {
    _headings: { state: true },
    _issues:   { state: true },
    _tree:     { state: true },
    _view:     { state: true },
  };

  #observer = null;

  constructor() {
    super();
    this._headings = [];
    this._issues   = [];
    this._tree     = [];
    this._view     = "tree";
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
    const headings    = collectHeadings(document.body);
    this._headings    = headings;
    this._issues      = analyzeHeadings(headings);
    this._tree        = buildTree(headings);
  }

  #observe() {
    this.#observer = new MutationObserver(() => this.#scan());
    this.#observer.observe(document.body, { childList: true, subtree: true, characterData: true });
  }

  render() {
    return html`
      <div class="wrap">
        <div class="toolbar">
          <span class="title">Page Headings</span>
          <div class="view-toggle">
            <button class="vbtn ${this._view === "tree" ? "active" : ""}" @click=${() => { this._view = "tree"; }}>Tree</button>
            <button class="vbtn ${this._view === "list" ? "active" : ""}" @click=${() => { this._view = "list"; }}>List</button>
          </div>
          <button class="refresh-btn" title="Refresh" @click=${() => this.#scan()}>↻ Refresh</button>
        </div>

        ${this._headings.length === 0
          ? html`<p class="empty">No headings found on this page.</p>`
          : this._view === "tree" ? this.#renderTree(this._tree, 0) : this.#renderList()}

        <div class="issues">
          <div class="issues-title">Analysis</div>
          ${this._issues.map(iss => html`
            <div class="issue issue--${iss.type}">
              <span class="issue-icon">${iss.type === "good" ? "✓" : iss.type === "warn" ? "⚠" : "ℹ"}</span>
              <span>${iss.msg}</span>
            </div>
          `)}
        </div>
      </div>
    `;
  }

  #renderTree(nodes, depth) {
    if (!nodes?.length) return "";
    return html`
      <ul class="tree" style="--depth:${depth}">
        ${nodes.map(n => html`
          <li class="tnode">
            <div class="tnode-row">
              <span class="hbadge hbadge--${n.level}">H${n.level}</span>
              <span class="htext">${n.text || html`<em class="empty-text">empty</em>`}</span>
            </div>
            ${n.children.length ? this.#renderTree(n.children, depth + 1) : ""}
          </li>
        `)}
      </ul>
    `;
  }

  #renderList() {
    return html`
      <ul class="flat-list">
        ${this._headings.map(h => html`
          <li class="flat-item">
            <span class="hbadge hbadge--${h.level}">H${h.level}</span>
            <span class="htext">${h.text || html`<em class="empty-text">empty</em>`}</span>
          </li>
        `)}
      </ul>
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
    .view-toggle { display: flex; gap: 2px; }
    .vbtn {
      padding: 3px 10px; font-size: 12px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; background: #fff; cursor: pointer;
    }
    .vbtn.active { background: var(--uui-color-current, #3544b1); color: #fff; border-color: transparent; }
    .refresh-btn {
      padding: 3px 10px; font-size: 12px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; background: #fff; cursor: pointer;
    }
    .refresh-btn:hover { background: var(--uui-color-surface-alt, #f5f5f5); }
    .empty { color: var(--uui-color-text-alt, #767676); font-size: 13px; margin: 0; }
    .tree { list-style: none; margin: 0; padding: 0 0 0 calc(var(--depth, 0) * 16px); }
    .tnode { margin: 2px 0; }
    .tnode-row { display: flex; align-items: baseline; gap: 8px; padding: 3px 0; }
    .flat-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
    .flat-item { display: flex; align-items: baseline; gap: 8px; }
    .hbadge {
      display: inline-block; min-width: 28px; text-align: center; padding: 1px 5px;
      border-radius: 3px; font-size: 11px; font-weight: 700; flex-shrink: 0;
    }
    .hbadge--1 { background: #1e3a8a; color: #fff; }
    .hbadge--2 { background: #1d4ed8; color: #fff; }
    .hbadge--3 { background: #3b82f6; color: #fff; }
    .hbadge--4 { background: #93c5fd; color: #1e3a8a; }
    .hbadge--5 { background: #bfdbfe; color: #1e3a8a; }
    .hbadge--6 { background: #dbeafe; color: #1e3a8a; }
    .htext { font-size: 13px; }
    .empty-text { color: var(--uui-color-text-alt, #767676); }
    .issues { border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 6px; overflow: hidden; }
    .issues-title {
      padding: 8px 12px; font-size: 12px; font-weight: 600;
      background: var(--uui-color-surface-alt, #f5f5f5);
      border-bottom: 1px solid var(--uui-color-border, #e3e3e3);
    }
    .issue { display: flex; align-items: flex-start; gap: 8px; padding: 8px 12px; font-size: 13px; }
    .issue + .issue { border-top: 1px solid var(--uui-color-border, #e3e3e3); }
    .issue--good { color: #15803d; }
    .issue--warn { color: #b45309; }
    .issue--info { color: #1d4ed8; }
    .issue-icon { flex-shrink: 0; font-weight: 700; }
  `];
}

customElements.define("dcms-seo-headers-property-editor", DcmsSeoHeadersPropertyEditor);
