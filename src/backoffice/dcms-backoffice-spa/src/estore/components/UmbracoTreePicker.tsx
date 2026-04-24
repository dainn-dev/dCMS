import { useEffect, useRef } from "react";

// Side-effect import: registers <dcms-content-picker> and <dcms-media-picker>
// custom elements on the global registry the first time this module is loaded.
import "./registerTreePickerElements";

type Props = {
  kind: "content" | "media";
  /** Current Guid (empty string = root / unpicked). */
  value: string;
  onChange: (next: string) => void;
  disabled?: boolean;
  className?: string;
};

/**
 * React wrapper around the `<dcms-content-picker>` / `<dcms-media-picker>` custom
 * elements (which in turn lazy-load Umbraco's `<umb-input-document|media>`).
 *
 * React 19 supports custom elements + their native `change` events, but its prop
 * mapping for unknown elements is "string-only" — to round-trip Guid values cleanly
 * and to subscribe to the bubbling `CustomEvent` we attach a ref-based listener.
 */
export function UmbracoTreePicker({ kind, value, onChange, disabled, className }: Props) {
  const ref = useRef<HTMLElement | null>(null);

  // Sync `value` prop → element property whenever React re-renders.
  useEffect(() => {
    const el = ref.current as (HTMLElement & { value?: string }) | null;
    if (!el) return;
    if (el.value !== value) el.value = value ?? "";
  }, [value]);

  // Subscribe to the element's `change` CustomEvent (bubbles up from the inner picker).
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const handler = (e: Event) => {
      const detail = (e as CustomEvent<{ value?: string }>).detail;
      onChange(detail?.value ?? (e.target as HTMLElement & { value?: string }).value ?? "");
    };
    el.addEventListener("change", handler as EventListener);
    return () => el.removeEventListener("change", handler as EventListener);
  }, [onChange]);

  const Tag = kind === "content" ? "dcms-content-picker" : "dcms-media-picker";

  // React's TS doesn't know about ref-typed custom-element JSX intrinsics — cast through any.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const El = Tag as any;
  return (
    <El
      ref={ref}
      class={className /* custom elements use `class`, not `className` */}
      style={disabled ? { pointerEvents: "none", opacity: 0.5 } : undefined}
    />
  );
}
