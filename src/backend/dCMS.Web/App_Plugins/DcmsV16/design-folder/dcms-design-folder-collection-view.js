import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_COLLECTION_CONTEXT } from "@umbraco-cms/backoffice/collection";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/document";
import { consumeContext } from "@umbraco-cms/backoffice/context-api";

/**
 * dcms-design-folder-collection-view
 *
 * Ports AngularJS designFolderListViewController + designFolderListView.html
 * to Umbraco v16 collectionView LitElement.
 *
 * Features:
 *  - Lists child Design documents as card grid
 *  - Shows baseline color swatches extracted from designBlockGrid property
 *  - Shows publish status, last edited date, updated by
 *  - "Set as Default Design" button — saves selectedDesign UDI on parent folder
 */
export class DcmsDesignFolderCollectionView extends LitElement {
  static properties = {
    _items:          { state: true },
    _selectedDesign: { state: true },
    _loading:        { state: true },
    _error:          { state: true },
    _saving:         { state: true },
  };

  #collectionContext = null;
  #workspaceContext  = null;
  #currentDocId      = null;

  constructor() {
    super();
    this._items          = [];
    this._selectedDesign = null;
    this._loading        = true;
    this._error          = null;
    this._saving         = false;
  }

  connectedCallback() {
    super.connectedCallback();

    consumeContext(this, UMB_DOCUMENT_WORKSPACE_CONTEXT, (ctx) => {
      this.#workspaceContext = ctx;
      this.#currentDocId = ctx?.getUnique?.() ?? null;
      this._loadChildren();
    });

    consumeContext(this, UMB_COLLECTION_CONTEXT, (ctx) => {
      this.#collectionContext = ctx;
      // Re-render when collection items change
      ctx?.items?.subscribe?.((items) => {
        if (items?.length) {
          this._mapItems(items);
        }
      });
    });
  }

  async _loadChildren() {
    if (!this.#currentDocId) return;
    this._loading = true;
    this._error   = null;
    try {
      const res = await fetch(
        `/umbraco/management/api/v1/document/${encodeURIComponent(this.#currentDocId)}/children?skip=0&take=100`,
        { credentials: "same-origin", headers: { "Accept": "application/json" } }
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      const items = data.items ?? data.value ?? [];
      this._mapItems(items);

      // Read selectedDesign from parent document values
      this._selectedDesign = this._readSelectedDesign();
    } catch (e) {
      this._error = e.message;
    } finally {
      this._loading = false;
    }
  }

  _mapItems(rawItems) {
    this._items = rawItems.map(item => {
      const variants = item.variants ?? [];
      const variant  = variants[0] ?? {};
      const props    = variant.segments?.[0]?.properties
        ?? variant.tabs?.flatMap?.(t => t.properties ?? [])
        ?? item.properties ?? [];

      // Extract baseline color swatches from designBlockGrid property
      const colors = this._extractColors(props);

      return {
        unique:       item.id ?? item.unique,
        udi:          `umb://document/${(item.id ?? item.unique)?.replace(/-/g, "")}`,
        name:         variant.name ?? item.name ?? "—",
        published:    item.variants?.[0]?.state === "Published" || item.published === true,
        updateDate:   item.lastModified ?? item.updateDate ?? null,
        updaterName:  item.updater?.name ?? item.lastModifiedBy?.name ?? null,
        colors,
      };
    });
  }

  _extractColors(props) {
    const designProp = props.find(p => p.alias === "designBlockGrid");
    if (!designProp?.value?.contentData) return [];

    try {
      const contentData = designProp.value.contentData;
      const stylingEntry = contentData.find(d => d.styling);
      const bgEntry      = contentData.find(d => d.backgroundColor);

      const swatches = (stylingEntry?.styling ?? [])
        .map(s => s.content?.background)
        .filter(Boolean)
        .slice(0, 7);

      if (bgEntry?.backgroundColor?.background) {
        swatches.unshift(bgEntry.backgroundColor.background);
      }

      return swatches.slice(0, 8);
    } catch {
      return [];
    }
  }

  _readSelectedDesign() {
    // Try to read from workspace context values
    try {
      const values = this.#workspaceContext?.getData?.()?.values ?? [];
      const generalProp = values.find(v => v.alias === "selectedDesign"
        || (v.alias === "general" && v.value?.selectedDesign));
      if (generalProp?.value?.selectedDesign) return generalProp.value.selectedDesign;
      if (generalProp?.value && typeof generalProp.value === "string") return generalProp.value;
    } catch {}
    return null;
  }

  async _setAsDefaultDesign(e, item) {
    e.stopPropagation();
    if (this._saving || !this.#currentDocId) return;
    this._saving = true;

    try {
      // Fetch current parent doc to update selectedDesign property
      const res = await fetch(
        `/umbraco/management/api/v1/document/${encodeURIComponent(this.#currentDocId)}`,
        { credentials: "same-origin", headers: { "Accept": "application/json" } }
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const doc = await res.json();

      // Find and patch the selectedDesign value
      const variants = doc.variants ?? [];
      for (const variant of variants) {
        const tabs = variant.tabs ?? variant.segments ?? [];
        for (const tab of tabs) {
          for (const prop of tab.properties ?? []) {
            if (prop.alias === "selectedDesign" ||
                (prop.alias === "general" && prop.value?.selectedDesign !== undefined)) {
              if (prop.alias === "general") {
                prop.value = { ...prop.value, selectedDesign: item.udi };
              } else {
                prop.value = item.udi;
              }
            }
          }
        }
      }

      // Publish
      const saveRes = await fetch(
        `/umbraco/management/api/v1/document/${encodeURIComponent(this.#currentDocId)}/publish`,
        {
          method: "PUT",
          credentials: "same-origin",
          headers: { "Content-Type": "application/json", "Accept": "application/json" },
          body: JSON.stringify({ publishSchedules: [] }),
        }
      );

      if (saveRes.ok) {
        this._selectedDesign = item.udi;
        this.dispatchEvent(new CustomEvent("dcms-design-selected", {
          bubbles: true, composed: true, detail: { udi: item.udi }
        }));
      } else {
        throw new Error(`Save failed: HTTP ${saveRes.status}`);
      }
    } catch (e) {
      console.error("[dcms-design-folder] setAsDefaultDesign error:", e);
    } finally {
      this._saving = false;
    }
  }

  _formatDate(val) {
    if (!val) return "—";
    try { return new Date(val).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" }); }
    catch { return String(val).slice(0, 16); }
  }

  _openItem(item) {
    if (!item?.unique) return;
    window.location.href = `/umbraco#/content/content/edit/${item.unique}`;
  }

  render() {
    if (this._loading) return html`
      <div class="loader-wrap"><uui-loader></uui-loader></div>
    `;

    if (this._error) return html`
      <div class="error">
        <uui-icon name="icon-alert"></uui-icon>
        Failed to load designs: ${this._error}
        <uui-button look="secondary" @click=${() => this._loadChildren()}>Retry</uui-button>
      </div>
    `;

    if (this._items.length === 0) return html`
      <div class="empty">No design documents found. Create a Design child node to get started.</div>
    `;

    return html`
      <div class="grid">
        ${this._items.map(item => this._renderCard(item))}
      </div>
    `;
  }

  _renderCard(item) {
    const isSelected = this._selectedDesign && item.udi === this._selectedDesign;
    return html`
      <div class="card ${isSelected ? "selected" : ""}" @click=${() => this._openItem(item)}>
        <div class="card-head">
          <uui-icon name="icon-brush" class="card-icon"></uui-icon>
          <div class="card-title">
            <span class="name">${item.name}</span>
            ${!item.published ? html`
              <uui-badge color="warning" look="badge">Unpublished</uui-badge>
            ` : ""}
          </div>
        </div>

        ${item.colors.length > 0 ? html`
          <div class="swatches">
            ${item.colors.map(color => html`
              <div class="swatch" style="background:${color}" title="${color}"></div>
            `)}
          </div>
        ` : html`<div class="swatches-empty">No color swatches</div>`}

        <ul class="meta">
          <li><span class="meta-label">Status</span><span class="meta-val">${item.published ? "Published" : "Draft"}</span></li>
          <li><span class="meta-label">Last edited</span><span class="meta-val">${this._formatDate(item.updateDate)}</span></li>
          ${item.updaterName ? html`
            <li><span class="meta-label">Updated by</span><span class="meta-val">${item.updaterName}</span></li>
          ` : ""}
        </ul>

        <div class="card-actions" @click=${(e) => e.stopPropagation()}>
          ${isSelected
            ? html`<uui-button look="primary" disabled>Default Design</uui-button>`
            : html`
              <uui-button look="secondary"
                ?disabled=${this._saving}
                @click=${(e) => this._setAsDefaultDesign(e, item)}>
                ${this._saving ? html`<uui-loader-circle></uui-loader-circle>` : ""}
                Set as Default Design
              </uui-button>
            `}
        </div>
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; padding: 16px; }

    /* Loading / error / empty */
    .loader-wrap { display: flex; justify-content: center; align-items: center; min-height: 200px; }
    .error {
      display: flex; align-items: center; gap: 10px; padding: 12px 16px;
      border-radius: 4px; background: #fff3f3; color: #c0392b;
      border: 1px solid #f5c6cb; font-size: 13px;
    }
    .empty {
      padding: 60px 0; text-align: center;
      color: var(--uui-color-text-alt,#767676); font-size: 13px; font-style: italic;
    }

    /* Grid */
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
      gap: 16px;
    }

    /* Card */
    .card {
      border: 2px solid var(--uui-color-border,#e3e3e3);
      border-radius: 8px; overflow: hidden;
      display: flex; flex-direction: column;
      cursor: pointer; transition: border-color 0.15s, box-shadow 0.15s;
      background: var(--uui-color-surface,#fff);
    }
    .card:hover {
      border-color: var(--uui-color-interactive,#1a73e8);
      box-shadow: 0 4px 12px rgba(0,0,0,0.08);
    }
    .card.selected {
      border-color: var(--uui-color-interactive,#1a73e8);
      background: #f5f9ff;
    }

    /* Card head */
    .card-head {
      display: flex; align-items: flex-start; gap: 8px;
      padding: 12px 12px 6px;
    }
    .card-icon { color: #6b4fa0; font-size: 20px; flex-shrink: 0; margin-top: 1px; }
    .card-title { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 0; }
    .name { font-size: 13px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

    /* Swatches */
    .swatches { display: flex; gap: 5px; padding: 6px 12px 8px; flex-wrap: wrap; }
    .swatch {
      width: 28px; height: 28px; border-radius: 4px;
      border: 1px solid rgba(0,0,0,0.1);
      flex-shrink: 0;
    }
    .swatches-empty { padding: 6px 12px 8px; font-size: 11px; color: var(--uui-color-text-alt,#aaa); font-style: italic; }

    /* Meta */
    .meta {
      list-style: none; margin: 0; padding: 6px 12px 8px;
      display: flex; flex-direction: column; gap: 3px;
      border-top: 1px solid var(--uui-color-border,#f0f0f0);
      flex: 1;
    }
    .meta li { display: flex; gap: 6px; font-size: 11px; }
    .meta-label { color: var(--uui-color-text-alt,#767676); min-width: 72px; }
    .meta-val { color: var(--uui-color-text,#333); font-weight: 500; }

    /* Actions */
    .card-actions {
      padding: 8px 12px 12px;
      border-top: 1px solid var(--uui-color-border,#f0f0f0);
    }
    .card-actions uui-button { width: 100%; }
  `];
}

customElements.define("dcms-design-folder-collection-view", DcmsDesignFolderCollectionView);
