import { useEffect, useState } from "react";

export type UmbracoLanguage = {
  isoCode: string;
  name: string;
  isDefault: boolean;
  isMandatory: boolean;
};

type State =
  | { status: "loading" }
  | { status: "ok"; languages: UmbracoLanguage[] }
  | { status: "error"; languages: UmbracoLanguage[] };

/**
 * Fetches the list of installed languages from the Umbraco Management API.
 * Endpoint: GET /umbraco/management/api/v1/language
 *
 * Falls back to `fallback` (default: EN only) when the API is unreachable
 * (e.g. during local SPA-only dev without Umbraco running).
 */
export function useUmbracoLanguages(
  fallback: UmbracoLanguage[] = [
    { isoCode: "en-US", name: "English", isDefault: true, isMandatory: true },
  ]
): State {
  const [state, setState] = useState<State>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;

    fetch("/umbraco/management/api/v1/language?skip=0&take=100", {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
    })
      .then(async (res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const json = await res.json();
        const items: UmbracoLanguage[] = (json.items ?? []).map(
          (l: Record<string, unknown>) => ({
            isoCode: String(l.isoCode ?? ""),
            name: String(l.name ?? l.isoCode ?? ""),
            isDefault: Boolean(l.isDefault),
            isMandatory: Boolean(l.isMandatory),
          })
        );
        if (!cancelled) {
          setState({ status: "ok", languages: items.length ? items : fallback });
        }
      })
      .catch(() => {
        if (!cancelled) {
          setState({ status: "error", languages: fallback });
        }
      });

    return () => {
      cancelled = true;
    };
  // fallback is stable between renders when passed as a literal — no dep needed
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return state;
}

/**
 * Derives a compact display label from an ISO code.
 * "en-US" → "EN-US", "vi" → "VI", "zh-CN" → "ZH-CN"
 * Capped at 5 characters to keep pills small.
 */
export function langLabel(isoCode: string): string {
  return isoCode.toUpperCase().slice(0, 5);
}
