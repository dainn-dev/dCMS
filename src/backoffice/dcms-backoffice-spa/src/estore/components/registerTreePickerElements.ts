/**
 * DAI-671 follow-up: Custom element wrappers around Umbraco v16 backoffice
 * tree pickers (`<umb-input-document>` and `<umb-input-media>`), so they can
 * be embedded inside a React UI rendered into the Umbraco backoffice.
 *
 * Why custom elements (vanilla, not Lit):
 *  - The SPA bundle does not depend on Lit; pulling it in (and the rest of
 *    the Umbraco backoffice peer-deps) would balloon the bundle by megabytes.
 *  - The Umbraco backoffice modules are already loaded by the backoffice
 *    runtime (the SPA is mounted *inside* the backoffice DOM), so a dynamic
 *    `import("@umbraco-cms/backoffice/document")` at element-connection time
 *    just resolves through the backoffice import map.
 *
 * Public API (TypeScript-side, used by React):
 *  - <dcms-content-picker value="<guid>" />  → fires `change` with detail.value
 *  - <dcms-media-picker   value="<guid>" />  → fires `change` with detail.value
 *
 * Both elements expose:
 *  - `value` attribute & property: a single Guid string (empty = root / unpicked)
 *  - `change` CustomEvent: { detail: { value: string } } whenever the user picks/clears
 *  - Graceful fallback: if the Umbraco backoffice module fails to load (e.g.
 *    SPA opened standalone), renders a plain text input so the user can still
 *    paste a Guid manually.
 *
 * The element is registered on first import (side-effect). React imports this
 * file in `useEffect` to avoid SSR/test crashes.
 */

type PickerKind = "content" | "media";

const TAG_BY_KIND: Record<PickerKind, string> = {
  content: "umb-input-document",
  media: "umb-input-media",
};

/** Dynamic import path → only resolved at runtime, not by Vite's static analysis. */
const MODULE_BY_KIND: Record<PickerKind, string> = {
  content: "@umbraco-cms/backoffice/document",
  media: "@umbraco-cms/backoffice/media",
};

class DcmsTreePickerBase extends HTMLElement {
  static observedAttributes = ["value"];

  #kind: PickerKind;
  #inner: HTMLElement | null = null;
  #fallback: HTMLInputElement | null = null;
  #ready = false;
  #pendingValue = "";

  constructor(kind: PickerKind) {
    super();
    this.#kind = kind;
  }

  get value(): string {
    return (this.#inner as unknown as { value?: string })?.value ?? this.#fallback?.value ?? this.#pendingValue ?? "";
  }
  set value(v: string) {
    const next = String(v ?? "");
    this.#pendingValue = next;
    if (this.#inner) (this.#inner as unknown as { value?: string }).value = next;
    if (this.#fallback) this.#fallback.value = next;
  }

  attributeChangedCallback(name: string, _old: string | null, next: string | null): void {
    if (name === "value") this.value = next ?? "";
  }

  connectedCallback(): void {
    if (this.#ready) return;
    this.#ready = true;
    this.#mount();
  }

  async #mount(): Promise<void> {
    // Try to load the real Umbraco backoffice picker module.
    try {
      // The backoffice import map exposes these specifiers at runtime — Vite
      // must NOT try to resolve them at build time, hence the `@vite-ignore`.
      const specifier = MODULE_BY_KIND[this.#kind];
      await import(/* @vite-ignore */ specifier);

      const tag = TAG_BY_KIND[this.#kind];
      const el = document.createElement(tag) as HTMLElement & { value?: string; max?: number };
      el.setAttribute("max", "1");
      if (this.#pendingValue) el.value = this.#pendingValue;

      // Forward the element's `change` event up so React can subscribe.
      el.addEventListener("change", () => {
        const v = (el as unknown as { value?: string }).value ?? "";
        this.dispatchEvent(new CustomEvent("change", { detail: { value: v }, bubbles: true, composed: true }));
      });

      this.#inner = el;
      this.replaceChildren(el);
      return;
    } catch {
      // Fallback: plain text input. Useful when SPA runs outside the backoffice.
    }

    const input = document.createElement("input");
    input.type = "text";
    input.placeholder = "Paste node Guid… (Umbraco picker unavailable)";
    input.style.cssText =
      "width:100%;padding:.45rem .6rem;border:1px solid rgba(0,0,0,.15);border-radius:.375rem;font-family:ui-monospace,monospace;font-size:.75rem";
    if (this.#pendingValue) input.value = this.#pendingValue;
    input.addEventListener("change", () => {
      this.dispatchEvent(
        new CustomEvent("change", { detail: { value: input.value }, bubbles: true, composed: true }),
      );
    });
    this.#fallback = input;
    this.replaceChildren(input);
  }
}

class DcmsContentPicker extends DcmsTreePickerBase {
  constructor() { super("content"); }
}
class DcmsMediaPicker extends DcmsTreePickerBase {
  constructor() { super("media"); }
}

if (typeof customElements !== "undefined") {
  if (!customElements.get("dcms-content-picker")) {
    customElements.define("dcms-content-picker", DcmsContentPicker);
  }
  if (!customElements.get("dcms-media-picker")) {
    customElements.define("dcms-media-picker", DcmsMediaPicker);
  }
}

// React's TypeScript JSX needs to know about the custom elements.
declare global {
  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace JSX {
    interface IntrinsicElements {
      "dcms-content-picker": React.DetailedHTMLProps<React.HTMLAttributes<HTMLElement>, HTMLElement> & {
        value?: string;
      };
      "dcms-media-picker": React.DetailedHTMLProps<React.HTMLAttributes<HTMLElement>, HTMLElement> & {
        value?: string;
      };
    }
  }
}

export {};
