/**
 * Backoffice UI tweaks:
 *  1. Hide built-in areas we do not use in dCMS.
 *  2. Inject dCMS brand theme — re-applied at multiple checkpoints to survive
 *     UUI CSS that loads asynchronously after onInit and overrides :root vars.
 */

const THEME_CSS = `
  :root {
    /* Header / top-bar (default: --uui-palette-space-cadet #1b264f) */
    --uui-palette-space-cadet:            #aa0014;
    --uui-palette-space-cadet-light:      #d12026;
    --uui-palette-space-cadet-dark:       #8a0010;
    --uui-color-header-surface:           #aa0014;
    --uui-color-header-contrast:          rgba(255,255,255,0.85);
    --uui-color-header-contrast-emphasis: #ffffff;
    /* Selected / active sidebar item */
    --uui-color-selected:                 #aa0014;
    --uui-color-selected-emphasis:        #d12026;
    --uui-color-selected-standalone:      #8a0010;
    --uui-color-selected-contrast:        #ffffff;
    /* Interactive links & default buttons */
    --uui-color-interactive:              #aa0014;
    --uui-color-interactive-emphasis:     #d12026;
    --uui-color-default:                  #aa0014;
    --uui-color-default-emphasis:         #d12026;
    --uui-color-default-standalone:       #8a0010;
    --uui-color-default-contrast:         #ffffff;
  }
`;

/** Singleton CSSStyleSheet so we always push the SAME reference to the end. */
let _sheet = null;

function applyTheme() {
  if ("adoptedStyleSheets" in document) {
    if (!_sheet) {
      _sheet = new CSSStyleSheet();
      _sheet.replaceSync(THEME_CSS);
    }
    // Remove any existing copy then push to end → always wins the cascade.
    document.adoptedStyleSheets = [
      ...document.adoptedStyleSheets.filter((s) => s !== _sheet),
      _sheet,
    ];
  } else {
    // Older-browser fallback: update or create a <style> element.
    let el = document.getElementById("dcms-theme");
    if (!el) {
      el = document.createElement("style");
      el.id = "dcms-theme";
      document.head.appendChild(el);
    }
    el.textContent = THEME_CSS;
  }
}

export const onInit = (_host, extensionRegistry) => {
  extensionRegistry.exclude("Umb.Dashboard.UmbracoNews");
  extensionRegistry.exclude("Umb.Section.Packages");
  /** Default header “help” (icon-help-alt) — replaced by dCMS “Thông báo” header app in umbraco-package.json */
  extensionRegistry.exclude("Umb.HeaderApp.Help");
  /** Built-in Users section — dCMS provides equivalent UI under Access → Users (DAI-669). */
  extensionRegistry.exclude("Umb.Section.Users");

  // Apply immediately, then re-apply after UUI CSS finishes loading
  // (it loads async and overrides :root vars set by an earlier onInit call).
  applyTheme();
  setTimeout(applyTheme, 0);    // next tick — after synchronous module evaluation
  setTimeout(applyTheme, 300);  // catches most async CSS injections
  setTimeout(applyTheme, 1500); // safety net for slow connections
};
