import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const EDITOR_SELECTOR = '[data-element="editor-container"]';

const BLOCK_CLASS_NAMES = [
  "banner-3","features-with-descriptions","team-members","case-study-card-2",
  "picture-with-description","section-gallery","section-review-3","swiper-reviews-2",
  "client-partners-3","client-partners-2","client-partners-1","cards-with-icons",
  "call-to-action","banner-2","banner","section-awards","three-col-section",
  "two-col-section","card-service","section-features","section-hero-2-backoffice",
  "section-hero-backoffice","footer-backoffice","designContentContent",
  "headding-block-design-grid","text-block-design-grid","block-image-design-grid",
  "buttons-block-design-grid","header-block-backoffice","umb-block-grid__layout-container",
  "styling-block-design-grid","design-background-design-grid","btn-primary",
  "btn-outline-primary","section-clients","umb-block-grid__layout-item",
  "callToAction-backoffice","buttons-block-backoffice","buttonsGroup-block-backoffice"
];

const VIEWS = [
  { label: "D", title: "Desktop", maxWidth: "100%" },
  { label: "T", title: "Tablet",  maxWidth: "992px" },
  { label: "M", title: "Mobile",  maxWidth: "400px" },
];

function traverseShadow(root, fn) {
  function walk(node) {
    if (!node) return;
    if (node.nodeType === Node.ELEMENT_NODE) {
      fn(node);
      if (node.shadowRoot) walk(node.shadowRoot);
    }
    if (node.childNodes) node.childNodes.forEach(walk);
  }
  walk(root);
}

function applyView(view) {
  const wrapper = document.querySelector(".umb-block-grid__wrapper");
  if (!wrapper) return;

  wrapper.style.transition  = "max-width 0.33s";
  wrapper.style.margin      = "0 auto";
  wrapper.style.overflowX   = "clip";
  wrapper.style.maxWidth    = view.maxWidth;

  const isMobile = view.title === "Mobile";
  const isTablet = view.title === "Tablet";

  document.body.classList.remove("view-tablet", "view-mobile", "view-default");
  if (isTablet)       document.body.classList.add("view-tablet");
  else if (isMobile)  document.body.classList.add("view-mobile");
  else                document.body.classList.add("view-default");

  const container = document.querySelector(EDITOR_SELECTOR);
  if (!container) return;

  traverseShadow(container, node => {
    for (const cls of BLOCK_CLASS_NAMES) {
      if (node.classList?.contains(cls)) {
        node.classList.toggle("view-mobile", isMobile);
        node.classList.toggle("view-tablet", isTablet);
      }
    }

    if (isMobile) {
      if (node.dataset?.contentElementTypeAlias === "designStyling" ||
          node.dataset?.contentElementTypeAlias === "designBackgroundBlock") {
        node.style.setProperty("--umb-block-grid--item-column-span", "12");
      }
    } else {
      if (node.dataset?.contentElementTypeAlias === "designStyling" ||
          node.dataset?.contentElementTypeAlias === "designBackgroundBlock") {
        node.style.setProperty("--umb-block-grid--item-column-span", "6");
      }
    }

    if (node.shadowRoot) {
      node.shadowRoot.querySelectorAll(".umb-block-grid__area").forEach(el => {
        el.classList.toggle("umb-block-grid__area-one-column", isMobile);
      });
    }
  });
}

export default class DcmsBlockGridWidthSwitcherPropertyEditor extends LitElement {
  static properties = {
    _active: { state: true },
  };

  #saveObserver = null;

  constructor() {
    super();
    this._active = 0;
  }

  connectedCallback() {
    super.connectedCallback();
    applyView(VIEWS[0]);
    this.#watchSave();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#saveObserver?.disconnect();
    this.#saveObserver = null;
    document.body.classList.remove("view-tablet", "view-mobile", "view-default");
  }

  #setView(idx) {
    this._active = idx;
    applyView(VIEWS[idx]);
  }

  #watchSave() {
    const target = document.querySelector(EDITOR_SELECTOR) ?? document.body;
    this.#saveObserver = new MutationObserver(() => {
      setTimeout(() => applyView(VIEWS[this._active]), 500);
    });
    this.#saveObserver.observe(target, { childList: true, subtree: false });
  }

  render() {
    return html`
      <div class="switcher">
        ${VIEWS.map((v, i) => html`
          <button class="vbtn ${i === this._active ? "active" : ""}"
            title=${v.title} @click=${() => this.#setView(i)}>
            ${v.label}
          </button>
        `)}
        <span class="label">${VIEWS[this._active].title}</span>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .switcher { display: flex; align-items: center; gap: 6px; }
    .vbtn {
      width: 36px; height: 32px; border: 2px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; background: #fff; cursor: pointer; font-weight: 700;
      font-size: 13px; transition: all .15s;
    }
    .vbtn:hover { border-color: var(--uui-color-current-emphasis, #5a67e8); }
    .vbtn.active {
      background: var(--uui-color-current, #3544b1); color: #fff;
      border-color: var(--uui-color-current, #3544b1);
    }
    .label { font-size: 12px; color: var(--uui-color-text-alt, #767676); margin-left: 4px; }
  `];
}

customElements.define("dcms-block-grid-width-switcher-property-editor", DcmsBlockGridWidthSwitcherPropertyEditor);
