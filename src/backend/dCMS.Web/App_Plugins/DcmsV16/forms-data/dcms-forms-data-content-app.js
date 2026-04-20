import { LitElement, html, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/document";
import { consumeContext } from "@umbraco-cms/backoffice/context-api";

/**
 * dcms-forms-data-content-app
 *
 * Ports AngularJS formsDataController + formsData.html to Umbraco v16 LitElement contentApp.
 *
 * Two tabs:
 *   Tab 1 — Email Input Field  → GET backoffice/ByteEditor/FormsDataApi/GetEmailInputFieldFormData?siteId=<key>
 *   Tab 2 — Contact Us Form    → GET backoffice/ByteEditor/FormsDataApi/getContactUsFormData?siteId=<key>
 *
 * Data is loaded lazily: Tab 1 on mount, Tab 2 on first switch.
 */
export class DcmsFormsDataContentApp extends LitElement {
  static properties = {
    _activeTab:          { state: true },
    _loading:            { state: true },
    _emailData:          { state: true },
    _contactData:        { state: true },
    _error:              { state: true },
    _siteId:             { state: true },
  };

  #workspaceContext = null;

  constructor() {
    super();
    this._activeTab   = "email";
    this._loading     = false;
    this._emailData   = null;
    this._contactData = null;
    this._error       = null;
    this._siteId      = null;
  }

  connectedCallback() {
    super.connectedCallback();
    // Resolve the document workspace context to get the current document's unique key
    consumeContext(this, UMB_DOCUMENT_WORKSPACE_CONTEXT, (ctx) => {
      this.#workspaceContext = ctx;
      // unique is the document key (GUID); use it as siteId for the API
      const unique = ctx?.getUnique?.() ?? null;
      if (unique) {
        this._siteId = unique;
        this._loadEmailData(unique);
      }
    });
  }

  async _loadEmailData(siteId) {
    this._loading = true;
    this._error   = null;
    try {
      const res = await fetch(
        `/backoffice/ByteEditor/FormsDataApi/GetEmailInputFieldFormData?siteId=${encodeURIComponent(siteId)}`,
        { credentials: "same-origin" }
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const text = await res.text();
      this._emailData = JSON.parse(text);
    } catch (e) {
      this._error = `Failed to load Email Input Field data: ${e.message}`;
    } finally {
      this._loading = false;
    }
  }

  async _loadContactData(siteId) {
    if (this._contactData !== null) return; // already loaded
    this._loading = true;
    this._error   = null;
    try {
      const res = await fetch(
        `/backoffice/ByteEditor/FormsDataApi/getContactUsFormData?siteId=${encodeURIComponent(siteId)}`,
        { credentials: "same-origin" }
      );
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const text = await res.text();
      this._contactData = JSON.parse(text);
    } catch (e) {
      this._error = `Failed to load Contact Us Form data: ${e.message}`;
    } finally {
      this._loading = false;
    }
  }

  _switchTab(tab) {
    this._activeTab = tab;
    if (tab === "contact" && this._siteId) {
      this._loadContactData(this._siteId);
    }
  }

  _formatDate(val) {
    if (!val) return "—";
    try { return new Date(val).toLocaleString(); } catch { return val; }
  }

  render() {
    return html`
      <div class="wrap">
        <!-- Tab nav -->
        <div class="tab-nav" role="tablist">
          <button
            role="tab"
            class="tab-btn ${this._activeTab === "email" ? "active" : ""}"
            @click=${() => this._switchTab("email")}
          >Email Input Field</button>
          <button
            role="tab"
            class="tab-btn ${this._activeTab === "contact" ? "active" : ""}"
            @click=${() => this._switchTab("contact")}
          >Contact Us Form</button>
        </div>

        <!-- Loading -->
        ${this._loading ? html`
          <div class="loader-wrap">
            <uui-loader></uui-loader>
          </div>
        ` : ""}

        <!-- Error -->
        ${this._error && !this._loading ? html`
          <div class="error">
            <uui-icon name="icon-alert"></uui-icon>
            ${this._error}
          </div>
        ` : ""}

        <!-- Tab 1: Email Input Field -->
        ${this._activeTab === "email" && !this._loading ? html`
          ${!this._emailData || this._emailData.length === 0 ? html`
            <div class="empty">No email input field submissions found.</div>
          ` : html`
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Form Name</th>
                    <th>Email</th>
                    <th>Date</th>
                    <th>IP</th>
                  </tr>
                </thead>
                <tbody>
                  ${this._emailData.map((row, i) => html`
                    <tr>
                      <td>${i + 1}</td>
                      <td>${row.FormName ?? "—"}</td>
                      <td>${row.Email ?? "—"}</td>
                      <td>${this._formatDate(row.Date)}</td>
                      <td>${row.IP ?? "—"}</td>
                    </tr>
                  `)}
                </tbody>
              </table>
            </div>
            <div class="count">${this._emailData.length} submission${this._emailData.length !== 1 ? "s" : ""}</div>
          `}
        ` : ""}

        <!-- Tab 2: Contact Us Form -->
        ${this._activeTab === "contact" && !this._loading ? html`
          ${!this._contactData || this._contactData.length === 0 ? html`
            <div class="empty">No contact form submissions found.</div>
          ` : html`
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>#</th>
                    <th>Form Name</th>
                    <th>First Name</th>
                    <th>Last Name</th>
                    <th>Full Name</th>
                    <th>Company</th>
                    <th>Phone</th>
                    <th>Email</th>
                    <th>Message</th>
                    <th>Date</th>
                    <th>IP</th>
                  </tr>
                </thead>
                <tbody>
                  ${this._contactData.map((row, i) => html`
                    <tr>
                      <td>${i + 1}</td>
                      <td>${row.FormName ?? "—"}</td>
                      <td>${row.FirstName ?? "—"}</td>
                      <td>${row.LastName ?? "—"}</td>
                      <td>${row.FullName ?? "—"}</td>
                      <td>${row.Company ?? "—"}</td>
                      <td>${row.PhoneNumber ?? "—"}</td>
                      <td>${row.Email ?? "—"}</td>
                      <td class="msg">${row.Message ?? "—"}</td>
                      <td>${this._formatDate(row.Date)}</td>
                      <td>${row.IP ?? "—"}</td>
                    </tr>
                  `)}
                </tbody>
              </table>
            </div>
            <div class="count">${this._contactData.length} submission${this._contactData.length !== 1 ? "s" : ""}</div>
          `}
        ` : ""}
      </div>
    `;
  }

  static styles = [UmbTextStyles, css`
    :host { display: block; padding: 16px; }
    .wrap { display: flex; flex-direction: column; gap: 12px; }

    /* Tab nav */
    .tab-nav { display: flex; gap: 0; border-bottom: 2px solid var(--uui-color-border,#e3e3e3); }
    .tab-btn {
      padding: 8px 18px; font-size: 13px; font-weight: 500;
      background: none; border: none; border-bottom: 2px solid transparent;
      margin-bottom: -2px; cursor: pointer;
      color: var(--uui-color-text-alt,#767676);
      transition: color 0.15s, border-color 0.15s;
    }
    .tab-btn:hover { color: var(--uui-color-text,#333); }
    .tab-btn.active {
      color: var(--uui-color-interactive,#1a73e8);
      border-bottom-color: var(--uui-color-interactive,#1a73e8);
      font-weight: 600;
    }

    /* Loader */
    .loader-wrap { display: flex; justify-content: center; align-items: center; min-height: 150px; }

    /* Error */
    .error {
      display: flex; align-items: center; gap: 8px;
      padding: 10px 14px; border-radius: 4px;
      background: #fff3f3; color: #c0392b; font-size: 13px;
      border: 1px solid #f5c6cb;
    }

    /* Empty */
    .empty {
      padding: 40px 0; text-align: center;
      color: var(--uui-color-text-alt,#767676); font-size: 13px; font-style: italic;
    }

    /* Table */
    .table-wrap { overflow-x: auto; border: 1px solid var(--uui-color-border,#e3e3e3); border-radius: 4px; }
    table { width: 100%; border-collapse: collapse; font-size: 12px; }
    thead { background: var(--uui-color-surface-alt,#f5f5f5); }
    th {
      text-align: left; padding: 8px 10px;
      font-weight: 600; font-size: 11px; text-transform: uppercase;
      color: var(--uui-color-text-alt,#767676);
      border-bottom: 1px solid var(--uui-color-border,#e3e3e3);
      white-space: nowrap;
    }
    td {
      padding: 7px 10px;
      border-bottom: 1px solid var(--uui-color-border,#f0f0f0);
      color: var(--uui-color-text,#333);
      vertical-align: top;
    }
    tr:last-child td { border-bottom: none; }
    tr:hover td { background: var(--uui-color-surface-alt,#fafafa); }
    td.msg { max-width: 200px; white-space: pre-wrap; word-break: break-word; }

    /* Count */
    .count { font-size: 11px; color: var(--uui-color-text-alt,#767676); text-align: right; }
  `];
}

customElements.define("dcms-forms-data-content-app", DcmsFormsDataContentApp);
