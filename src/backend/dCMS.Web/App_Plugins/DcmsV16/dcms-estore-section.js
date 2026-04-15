import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

const pages = [
  { id: "brands", label: "Brands", icon: "icon-tags" },
  { id: "categories", label: "Categories", icon: "icon-ordered-list" },
  { id: "products", label: "Products", icon: "icon-box" },
  { id: "attributes", label: "Attributes", icon: "icon-edit" },
  { id: "promocodes", label: "Promocodes", icon: "icon-ticket" },
  { id: "fulfillment-options", label: "Fulfillment options", icon: "icon-truck" },
];

export default class DcmsEStoreSectionElement extends UmbElementMixin(HTMLElement) {
  #active = pages[0].id;

  constructor() {
    super();
    this.attachShadow({ mode: "open" });
  }

  connectedCallback() {
    this.#render();
  }

  #render() {
    const p = pages.find((x) => x.id === this.#active) ?? pages[0];
    this.shadowRoot.innerHTML = `
      <style>
        :host { display: block; height: 100%; box-sizing: border-box; }
        .wrap { display: flex; min-height: 100%; font-family: system-ui, sans-serif; }
        nav {
          width: 220px; flex-shrink: 0; border-right: 1px solid var(--uui-color-border, #e9e9eb);
          padding: 0.75rem; background: var(--uui-color-surface-alt, #f8f8f8);
        }
        nav h2 { font-size: 0.9rem; margin: 0 0 0.75rem; color: #281716; }
        button.nav {
          display: flex; align-items: center; gap: 0.5rem; width: 100%; text-align: left;
          border: none; background: transparent; padding: 0.45rem 0.5rem; border-radius: 4px;
          cursor: pointer; font-size: 0.85rem; color: #1b264f;
        }
        button.nav:hover { background: #e8eaf3; }
        button.nav.on { background: #f5c1bc; font-weight: 600; }
        main { flex: 1; padding: 1rem 1.25rem; min-width: 0; }
      </style>
      <div class="wrap">
        <nav>
          <h2>eStore</h2>
          ${pages
            .map(
              (x) =>
                `<button type="button" class="nav${x.id === p.id ? " on" : ""}" data-id="${x.id}">${x.label}</button>`
            )
            .join("")}
        </nav>
        <main>
          <uui-box headline="${p.label}">
            <p style="margin:0 0 0.5rem; color:#64748b; font-size:0.9rem;">
              Placeholder workspace for <strong>${p.label}</strong>. Wire this view to your Catalog API when ready.
            </p>
            <uui-button look="secondary" label="Refresh" disabled></uui-button>
          </uui-box>
        </main>
      </div>`;

    this.shadowRoot.querySelectorAll("button.nav").forEach((btn) => {
      btn.addEventListener("click", () => {
        this.#active = btn.getAttribute("data-id");
        this.#render();
      });
    });
  }
}

customElements.define("dcms-estore-section", DcmsEStoreSectionElement);
