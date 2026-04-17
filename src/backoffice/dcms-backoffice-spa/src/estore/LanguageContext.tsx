import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { UmbracoLanguage } from "./useUmbracoLanguages";

// ── Context ───────────────────────────────────────────────────────────────────

type LanguageContextValue = {
  languages: UmbracoLanguage[];
  loading: boolean;
};

const DEFAULT_LANGUAGE: UmbracoLanguage = {
  isoCode: "en-US",
  name: "English",
  isDefault: true,
  isMandatory: true,
};

const LanguageContext = createContext<LanguageContextValue>({
  languages: [DEFAULT_LANGUAGE],
  loading: false,
});

// ── Provider ──────────────────────────────────────────────────────────────────
// Languages are pre-fetched by the Lit host element (dcms-estore-section.js)
// which has proper access to UMB_AUTH_CONTEXT. React never calls the
// Management API directly — it just consumes what the host provides.

export function LanguageProvider({
  languages: initialLanguages,
  children,
}: {
  languages?: UmbracoLanguage[];
  children: ReactNode;
}) {
  const value = useMemo<LanguageContextValue>(() => {
    const langs =
      initialLanguages && initialLanguages.length > 0
        ? initialLanguages
        : [DEFAULT_LANGUAGE];
    return { languages: langs, loading: false };
  }, [initialLanguages]);

  return (
    <LanguageContext.Provider value={value}>
      {children}
    </LanguageContext.Provider>
  );
}

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useLanguages(): LanguageContextValue {
  return useContext(LanguageContext);
}
