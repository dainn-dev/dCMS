import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

const HANGFIRE_URL = "/umbraco/dcms/hangfire";

// Match the dCMS backoffice SPA palette (tailwind.config.js — Material Design red/blush tones).
// Pulled directly from src/backoffice/dcms-backoffice-spa/tailwind.config.js so the embedded
// Hangfire dashboard visually merges with the surrounding SPA pages.
//
//   background / surface            #fff8f7   page surface (warm off-white)
//   surface-container-lowest        #ffffff   tile surface
//   surface-container-low           #fff0ef   subdued surface (sidebars, headers)
//   surface-container               #ffe9e7   stronger subdued surface (footer / striped rows)
//   surface-container-high          #ffe2de   hover state
//   surface-variant                 #fbdbd8   accent backgrounds
//   outline                         #916f6c   strong border / muted text
//   outline-variant                 #e5bdb9   1px borders
//   primary                         #aa0014   primary accent (deep red)
//   primary-container               #d12026   primary hover / active
//   on-primary                      #ffffff
//   on-surface                      #281716   primary text (near-black)
//   on-surface-variant              #5c403d   secondary text
//   secondary                       #a53a35   alt accent
//   tertiary                        #005880   info/link
//   tertiary-container              #0072a3   info hover
//   error                           #ba1a1a
const LIGHT_OVERRIDE_CSS = `
  :root, html { color-scheme: light; }
  html, body { background: #fff8f7 !important; color: #281716 !important; font-family: Inter, system-ui, sans-serif !important; }
  body, .container-fluid, .content-wrapper { background: #fff8f7 !important; color: #281716 !important; }

  /* Top tabs / navbar */
  .navbar, .navbar-inverse, .navbar-default {
    background: #fff0ef !important; border-color: #e5bdb9 !important; color: #281716 !important;
  }
  .navbar a, .navbar .navbar-brand, .navbar-nav > li > a { color: #281716 !important; }
  .navbar-nav > li > a:hover { background: #ffe9e7 !important; color: #281716 !important; }
  .navbar-nav > .active > a, .navbar-nav > .active > a:hover, .navbar-nav > .active > a:focus {
    background: #fff8f7 !important; color: #aa0014 !important; border-bottom: 2px solid #aa0014 !important;
  }

  /* Side menu (Jobs / Servers pages) — Hangfire renders Bootstrap .list-group */
  .list-group, .js-jobs-menu, .nav-stacked, .nav-pills, aside, .col-md-3 .nav, .col-md-3 .list-group {
    background: #fff0ef !important; border-radius: 6px !important; border: 1px solid #e5bdb9 !important;
  }
  .list-group-item, .js-jobs-menu .list-group-item,
  .nav-stacked > li > a, .nav-pills > li > a, aside a {
    background: #fff0ef !important; color: #aa0014 !important; border-color: #e5bdb9 !important;
  }
  .list-group-item:hover, .js-jobs-menu .list-group-item:hover,
  .nav-stacked > li > a:hover, .nav-pills > li > a:hover {
    background: #ffe9e7 !important; color: #281716 !important;
  }
  .list-group-item.active, .list-group-item.active:hover, .list-group-item.active:focus,
  .js-jobs-menu .list-group-item.active,
  .nav-stacked > li.active > a, .nav-pills > li.active > a,
  .nav-stacked > li.active > a:hover, .nav-pills > li.active > a:hover {
    background: #aa0014 !important; color: #ffffff !important; border-color: #aa0014 !important;
  }
  .list-group-item .badge, .js-jobs-menu .badge,
  .nav-stacked .badge, .nav-pills .badge {
    background: #ffffff !important; color: #281716 !important; border: 1px solid #e5bdb9 !important;
  }
  .list-group-item.active .badge { background: #ffffff !important; color: #aa0014 !important; border-color: #ffffff !important; }

  /* Metric cards (Overview tiles) */
  .metric {
    background: #ffffff !important; border: 1px solid #e5bdb9 !important; color: #281716 !important;
    border-radius: 8px !important;
  }
  .metric-title { color: #5c403d !important; }
  .metric-value, .metric > p:first-child { color: #281716 !important; }
  .metric.highlighted { background: #ffe2de !important; border-color: #aa0014 !important; }
  .metric.metric-warning, .metric.warning { background: #fff5d4 !important; border-color: #b08600 !important; }
  .metric.metric-danger, .metric.danger { background: #ffdada !important; border-color: #ba1a1a !important; }

  /* Panels & alerts */
  .panel, .panel-default { background: #ffffff !important; color: #281716 !important; border-color: #e5bdb9 !important; }
  .panel-heading { background: #fff0ef !important; color: #281716 !important; border-color: #e5bdb9 !important; }
  .panel-body { background: #ffffff !important; color: #281716 !important; }
  .alert { background: #fff0ef !important; color: #281716 !important; border-color: #e5bdb9 !important; }
  .alert-warning { background: #fff5d4 !important; color: #5b4400 !important; border-color: #d6a800 !important; }
  .alert-info { background: #d8edf7 !important; color: #003952 !important; border-color: #0072a3 !important; }
  .alert-success { background: #d9efe1 !important; color: #1b4d2c !important; border-color: #2f7a44 !important; }
  .alert-danger { background: #ffdada !important; color: #930a0a !important; border-color: #ba1a1a !important; }

  /* Tables */
  table, .table { background: #ffffff !important; color: #281716 !important; }
  .table > thead > tr > th { background: #fff0ef !important; color: #281716 !important; border-color: #e5bdb9 !important; }
  .table > tbody > tr > td { background: #ffffff !important; color: #281716 !important; border-color: #e5bdb9 !important; }
  .table-striped > tbody > tr:nth-of-type(odd) > * { background: #fff8f7 !important; }
  .table-hover > tbody > tr:hover > * { background: #ffe9e7 !important; }

  /* Buttons */
  .btn-default {
    background: #ffffff !important; color: #281716 !important; border: 1px solid #e5bdb9 !important;
  }
  .btn-default:hover, .btn-default:focus { background: #fff0ef !important; border-color: #aa0014 !important; }
  .btn-primary {
    background: #aa0014 !important; border-color: #aa0014 !important; color: #ffffff !important;
  }
  .btn-primary:hover, .btn-primary:focus { background: #d12026 !important; border-color: #d12026 !important; }
  .btn-danger { background: #ba1a1a !important; border-color: #ba1a1a !important; color: #ffffff !important; }
  .btn-success { background: #2f7a44 !important; border-color: #2f7a44 !important; color: #ffffff !important; }

  /* Misc */
  pre, code { background: #fff0ef !important; color: #281716 !important; border: 1px solid #e5bdb9 !important; }
  .label-default { background: #5c403d !important; color: #ffffff !important; }
  .label-primary { background: #aa0014 !important; color: #ffffff !important; }
  .label-success { background: #2f7a44 !important; color: #ffffff !important; }
  .label-warning { background: #d6a800 !important; color: #ffffff !important; }
  .label-danger { background: #ba1a1a !important; color: #ffffff !important; }
  .text-muted { color: #5c403d !important; }
  a { color: #aa0014 !important; }
  a:hover { color: #d12026 !important; }
  hr { border-color: #e5bdb9 !important; }

  /* Hangfire footer (version + storage info bar) — hidden inside the embedded backoffice view. */
  footer, .footer, .navbar-fixed-bottom, #footer { display: none !important; }

  /* Page title (h1, page-header) */
  h1, h2, h3, h4, h5, h6, .page-header { color: #281716 !important; border-color: #e5bdb9 !important; }

  /* Realtime/History graph axis labels & series */
  .ct-label { color: #5c403d !important; fill: #5c403d !important; }
  .ct-grid { stroke: #e5bdb9 !important; }
  .ct-series-a .ct-line, .ct-series-a .ct-point { stroke: #aa0014 !important; }
  .ct-series-b .ct-line, .ct-series-b .ct-point { stroke: #2f7a44 !important; }
`;

// Layout strategy: iframe fills the available viewport height and scrolls internally.
// This makes Hangfire's .navbar-fixed-bottom footer pin to the bottom of the iframe viewport
// at all times (instead of trailing the document), which is the user-requested behavior.
//
// We also add safe bottom padding to body so content scrolling under the fixed footer can be
// scrolled fully into view.
const LAYOUT_FIX_CSS = `
  html, body {
    height: 100% !important;
    min-height: 100% !important;
    overflow-x: hidden !important;
    overflow-y: auto !important;
    padding-bottom: 0 !important;
    margin-bottom: 0 !important;
  }
`;

function applyLightTheme(iframe) {
  try {
    const doc = iframe.contentDocument;
    if (!doc) return;
    if (!doc.getElementById("dcms-hangfire-light-override")) {
      const style = doc.createElement("style");
      style.id = "dcms-hangfire-light-override";
      style.textContent = LIGHT_OVERRIDE_CSS + LAYOUT_FIX_CSS;
      (doc.head || doc.documentElement).appendChild(style);
    }
    if (doc.documentElement) doc.documentElement.style.colorScheme = "light";
  } catch {
    // Cross-origin or transient timing — ignore.
  }
}

// Hangfire's storage-info footer markup varies across versions (sometimes
// <nav class="navbar-fixed-bottom">, sometimes a plain trailing block). CSS rules above
// hide the standard cases; this JS hides the version-text variant by content match.
function hideHangfireFooter(iframe) {
  try {
    const doc = iframe.contentDocument;
    if (!doc || !doc.body) return;

    const candidates = new Set([
      ...doc.querySelectorAll(".navbar-fixed-bottom, footer, .footer, #footer, body > nav.navbar"),
    ]);
    // Scan the last ~4 children of body for an element containing "Hangfire <version>" + timing text.
    const tail = Array.from(doc.body.children).slice(-4);
    for (const el of tail) {
      const t = (el.textContent || "").trim();
      if (/Hangfire\s+\d/i.test(t) && /Storage Time|SQL Server|Generated/i.test(t)) {
        candidates.add(el);
      }
    }

    for (const el of candidates) {
      if (el instanceof HTMLElement) el.style.setProperty("display", "none", "important");
    }
  } catch {
    // ignore cross-origin or detached iframe
  }
}

function fitToViewport(host, iframe) {
  if (!host || !iframe) return;
  // Distance from the element's top to the bottom of the visible viewport — gives the exact
  // height available to the iframe without overflowing the backoffice scroll area.
  const top = host.getBoundingClientRect().top;
  const available = Math.max(window.innerHeight - top - 8, 360);
  host.style.height = available + "px";
  iframe.style.height = available + "px";
}

export default class DcmsHangfireDashboardElement extends UmbElementMixin(HTMLElement) {
  /** @type {HTMLIFrameElement | null} */
  #iframe = null;
  /** @type {(() => void) | null} */
  #onLoad = null;
  /** @type {(() => void) | null} */
  #onResize = null;
  /** @type {ResizeObserver | null} */
  #parentObserver = null;

  connectedCallback() {
    // Fill the visible backoffice area exactly — measure available height from the element's
    // top offset to the bottom of the window, instead of using calc(100vh - …) constants.
    // Using a constant offset like 120px tends to overshoot whenever the backoffice chrome
    // changes height (notifications, banner messages), forcing the host page to scroll AND
    // pushing Hangfire's fixed footer below the viewport.
    this.style.display = "block";
    this.style.position = "relative";
    this.style.width = "100%";
    this.style.overflow = "hidden";

    this.innerHTML = `
      <iframe
        src="${HANGFIRE_URL}"
        style="display:block;width:100%;border:0;background:#fff8f7"
        referrerpolicy="same-origin"
        loading="eager"
        title="Hangfire dashboard"></iframe>`;

    this.#iframe = this.querySelector("iframe");
    if (!this.#iframe) return;

    // Bind iframe to the visible area below its current top offset.
    fitToViewport(this, this.#iframe);
    // Re-fit on viewport resize and whenever the parent (Umbraco workspace area) resizes.
    this.#onResize = () => fitToViewport(this, this.#iframe);
    window.addEventListener("resize", this.#onResize, { passive: true });
    if (typeof ResizeObserver !== "undefined" && this.parentElement) {
      this.#parentObserver = new ResizeObserver(this.#onResize);
      this.#parentObserver.observe(this.parentElement);
    }
    // First render: backoffice chrome may still be settling — re-fit a few times.
    setTimeout(() => fitToViewport(this, this.#iframe), 100);
    setTimeout(() => fitToViewport(this, this.#iframe), 500);

    this.#onLoad = () => {
      const f = this.#iframe;
      if (!f) return;
      applyLightTheme(f);
      hideHangfireFooter(f);
      // Hangfire renders graphs/badges asynchronously — re-apply theme + pin footer
      // to cover post-mount renders and any inline-style writes Hangfire performs.
      setTimeout(() => { applyLightTheme(f); hideHangfireFooter(f); }, 250);
      setTimeout(() => { applyLightTheme(f); hideHangfireFooter(f); }, 1000);
      setTimeout(() => hideHangfireFooter(f), 2500);

      // Watch for body mutations (Hangfire repaints partials on poll) and re-pin.
      try {
        const doc = f.contentDocument;
        if (doc && doc.body && typeof MutationObserver !== "undefined") {
          const mo = new MutationObserver(() => hideHangfireFooter(f));
          mo.observe(doc.body, { childList: true, subtree: false, attributes: true, attributeFilter: ["style", "class"] });
          // Stash on instance via closure — disconnected on disconnectedCallback via this.#parentObserver path.
          this.__mo = mo;
        }
      } catch { /* ignore */ }
    };
    this.#iframe.addEventListener("load", this.#onLoad);
  }

  disconnectedCallback() {
    if (this.#iframe && this.#onLoad) this.#iframe.removeEventListener("load", this.#onLoad);
    if (this.#onResize) window.removeEventListener("resize", this.#onResize);
    this.#parentObserver?.disconnect();
    this.#parentObserver = null;
    if (this.__mo) { try { this.__mo.disconnect(); } catch { /* ignore */ } this.__mo = null; }
    this.#iframe = null;
    this.#onLoad = null;
    this.#onResize = null;
    this.innerHTML = "";
  }
}

customElements.define("dcms-hangfire-dashboard", DcmsHangfireDashboardElement);
