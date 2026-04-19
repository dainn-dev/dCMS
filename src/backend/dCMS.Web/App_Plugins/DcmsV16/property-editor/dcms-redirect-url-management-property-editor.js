import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

const API_BASE = "/backoffice/ByteEditor/CustomRedirectsApi";

function getSiteId() {
  const m = location.pathname.match(/\/content\/([^/]+)/);
  return m ? m[1] : "";
}

export default class DcmsRedirectUrlManagementPropertyEditor extends LitElement {
  static properties = {
    _rows:    { state: true },
    _loading: { state: true },
    _error:   { state: true },
    _saving:  { state: true },
    _editing: { state: true },
    _form:    { state: true },
  };

  constructor() {
    super();
    this._rows    = [];
    this._loading = false;
    this._error   = null;
    this._saving  = false;
    this._editing = null;
    this._form    = this.#emptyForm();
  }

  connectedCallback() {
    super.connectedCallback();
    this.#load();
  }

  #emptyForm() {
    return { OriginalUrl: "", RedirectedTo: "", RedirectType: "301" };
  }

  async #load() {
    this._loading = true;
    this._error   = null;
    try {
      const siteId = getSiteId();
      const res = await fetch(`${API_BASE}/GetAll?siteId=${encodeURIComponent(siteId)}`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      this._rows = await res.json();
    } catch (e) {
      this._error = e.message;
    } finally {
      this._loading = false;
    }
  }

  async #save() {
    const { OriginalUrl, RedirectedTo, RedirectType } = this._form;
    if (!OriginalUrl.trim() || !RedirectedTo.trim()) return;
    this._saving = true;
    try {
      const siteId = getSiteId();
      const body = { SiteId: siteId, OriginalUrl, RedirectedTo, RedirectType };
      if (this._editing != null) body.Id = this._editing;
      const endpoint = this._editing != null ? "Update" : "Add";
      const res = await fetch(`${API_BASE}/${endpoint}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      this._editing = null;
      this._form    = this.#emptyForm();
      await this.#load();
    } catch (e) {
      this._error = e.message;
    } finally {
      this._saving = false;
    }
  }

  async #delete(id) {
    if (!confirm("Delete this redirect?")) return;
    try {
      const res = await fetch(`${API_BASE}/Delete?id=${encodeURIComponent(id)}`, { method: "POST" });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      await this.#load();
    } catch (e) {
      this._error = e.message;
    }
  }

  #edit(row) {
    this._editing = row.Id;
    this._form    = { OriginalUrl: row.OriginalUrl, RedirectedTo: row.RedirectedTo, RedirectType: String(row.RedirectType) };
  }

  #cancel() {
    this._editing = null;
    this._form    = this.#emptyForm();
  }

  #field(key, val) {
    this._form = { ...this._form, [key]: val };
  }

  render() {
    return html`
      <div class="wrap">
        <div class="form-area">
          <h4 class="form-title">${this._editing != null ? "Edit Redirect" : "Add Redirect"}</h4>
          <div class="form-row">
            <div class="form-group">
              <label>From URL</label>
              <input type="text" placeholder="/old-path" .value=${this._form.OriginalUrl}
                @input=${e => this.#field("OriginalUrl", e.target.value)} />
            </div>
            <div class="form-group">
              <label>To URL</label>
              <input type="text" placeholder="/new-path or https://..." .value=${this._form.RedirectedTo}
                @input=${e => this.#field("RedirectedTo", e.target.value)} />
            </div>
            <div class="form-group form-group--sm">
              <label>Type</label>
              <select .value=${this._form.RedirectType}
                @change=${e => this.#field("RedirectType", e.target.value)}>
                <option value="301">301</option>
                <option value="302">302</option>
              </select>
            </div>
          </div>
          <div class="form-actions">
            <button class="btn btn-primary" ?disabled=${this._saving} @click=${() => this.#save()}>
              ${this._saving ? "Saving…" : (this._editing != null ? "Update" : "Add")}
            </button>
            ${this._editing != null ? html`<button class="btn" @click=${() => this.#cancel()}>Cancel</button>` : ""}
          </div>
        </div>

        ${this._error ? html`<p class="err">${this._error}</p>` : ""}
        ${this._loading ? html`<p class="loading">Loading…</p>` : ""}

        ${!this._loading && this._rows.length === 0
          ? html`<p class="empty">No redirects configured.</p>`
          : html`
          <table class="tbl">
            <thead>
              <tr>
                <th>From</th><th>To</th><th>Type</th><th>Actions</th>
              </tr>
            </thead>
            <tbody>
              ${this._rows.map(r => html`
                <tr class="${r.Id === this._editing ? "editing-row" : ""}">
                  <td class="url-cell">${r.OriginalUrl}</td>
                  <td class="url-cell">${r.RedirectedTo}</td>
                  <td><span class="badge badge-${r.RedirectType}">${r.RedirectType}</span></td>
                  <td class="actions-cell">
                    <button class="icon-btn" title="Edit" @click=${() => this.#edit(r)}>✏️</button>
                    <button class="icon-btn icon-btn--danger" title="Delete" @click=${() => this.#delete(r.Id)}>🗑</button>
                  </td>
                </tr>
              `)}
            </tbody>
          </table>
        `}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; }
    .wrap { display: flex; flex-direction: column; gap: 12px; }
    .form-area {
      border: 1px solid var(--uui-color-border, #e3e3e3); border-radius: 6px;
      padding: 14px; background: var(--uui-color-surface-alt, #f5f5f5);
    }
    .form-title { margin: 0 0 10px; font-size: 13px; font-weight: 600; }
    .form-row { display: flex; gap: 10px; flex-wrap: wrap; }
    .form-group { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 160px; }
    .form-group--sm { flex: 0 0 80px; min-width: 70px; }
    .form-group label { font-size: 12px; font-weight: 600; }
    .form-group input, .form-group select {
      padding: 6px 8px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; font-size: 13px; background: #fff;
    }
    .form-actions { display: flex; gap: 8px; margin-top: 10px; }
    .btn {
      padding: 6px 14px; border: 1px solid var(--uui-color-border, #e3e3e3);
      border-radius: 4px; font-size: 13px; cursor: pointer; background: #fff;
    }
    .btn-primary {
      background: var(--uui-color-current, #3544b1); color: #fff; border-color: transparent;
    }
    .btn-primary:hover { background: #2a3699; }
    .btn:disabled { opacity: .5; cursor: not-allowed; }
    .err  { color: #c0392b; font-size: 13px; margin: 0; }
    .loading, .empty { color: var(--uui-color-text-alt, #767676); font-size: 13px; margin: 0; }
    .tbl { width: 100%; border-collapse: collapse; font-size: 13px; }
    .tbl th {
      text-align: left; padding: 8px 10px; font-size: 12px; font-weight: 600;
      border-bottom: 2px solid var(--uui-color-border, #e3e3e3);
      background: var(--uui-color-surface-alt, #f5f5f5);
    }
    .tbl td { padding: 8px 10px; border-bottom: 1px solid var(--uui-color-border, #e3e3e3); }
    .tbl tr:last-child td { border-bottom: none; }
    .tbl tr:hover td { background: var(--uui-color-surface-alt, #f5f5f5); }
    .editing-row td { background: #f0f2ff !important; }
    .url-cell { word-break: break-all; max-width: 260px; }
    .badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 700; }
    .badge-301 { background: #e8f0fe; color: #1a56db; }
    .badge-302 { background: #fef9e7; color: #d97706; }
    .actions-cell { white-space: nowrap; }
    .icon-btn { background: none; border: none; cursor: pointer; padding: 2px 4px; font-size: 14px; }
    .icon-btn--danger:hover { color: #c0392b; }
  `];
}

customElements.define("dcms-redirect-url-management-property-editor", DcmsRedirectUrlManagementPropertyEditor);
