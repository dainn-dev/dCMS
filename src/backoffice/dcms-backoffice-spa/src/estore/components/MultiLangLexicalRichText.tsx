import { useCallback, useEffect, useRef, useState } from "react";
import {
  IconFormatBold,
  IconFormatItalic,
  IconFormatListBulleted,
  IconFormatUnderlined,
  IconImage,
  IconLink,
} from "../../orders/icons";
import {
  labelBase,
  LangSwitcher,
  langKey,
  useLangField,
} from "./MultiLangField";

type Props = {
  label: string;
  defaultValues?: Record<string, string>;
  onValuesChange?: (values: Record<string, string>) => void;
  placeholders?: Record<string, string>;
  hint?: string;
  autoFocus?: boolean;
  className?: string;
};

function NativeRichText({
  initialHtml,
  placeholder,
  autoFocus,
  onHtml,
  minHeight = 120,
}: {
  initialHtml: string;
  placeholder?: string;
  autoFocus?: boolean;
  onHtml: (html: string) => void;
  minHeight?: number;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [isEmpty, setIsEmpty] = useState(!initialHtml.trim());
  const [isBold, setIsBold] = useState(false);
  const [isItalic, setIsItalic] = useState(false);
  const [isUnderline, setIsUnderline] = useState(false);

  useEffect(() => {
    if (ref.current) {
      ref.current.innerHTML = initialHtml;
      setIsEmpty(!initialHtml.trim());
    }
    if (autoFocus) ref.current?.focus();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const syncState = useCallback(() => {
    setIsBold(document.queryCommandState("bold"));
    setIsItalic(document.queryCommandState("italic"));
    setIsUnderline(document.queryCommandState("underline"));
  }, []);

  const handleInput = useCallback(() => {
    const el = ref.current;
    if (!el) return;
    const html = el.innerHTML;
    setIsEmpty(!(el.textContent?.trim()));
    onHtml(html === "<br>" ? "" : html);
  }, [onHtml]);

  const fmt = useCallback((cmd: string) => {
    ref.current?.focus();
    document.execCommand(cmd, false, undefined);
    syncState();
  }, [syncState]);

  const bulletList = useCallback(() => {
    ref.current?.focus();
    document.execCommand("insertUnorderedList", false, undefined);
  }, []);

  const addLink = useCallback(() => {
    const url = window.prompt("Link URL");
    if (!url?.trim()) return;
    ref.current?.focus();
    document.execCommand("createLink", false, url.trim());
  }, []);

  const addImageLink = useCallback(() => {
    const url = window.prompt("Image URL");
    if (!url?.trim()) return;
    ref.current?.focus();
    const sel = window.getSelection();
    if (sel && sel.rangeCount > 0) {
      const range = sel.getRangeAt(0);
      const a = document.createElement("a");
      a.href = url.trim();
      a.target = "_blank";
      a.rel = "noreferrer";
      a.textContent = "🖼️ Image";
      range.deleteContents();
      range.insertNode(a);
      range.setStartAfter(a);
      range.collapse(true);
      sel.removeAllRanges();
      sel.addRange(range);
    }
    handleInput();
  }, [handleInput]);

  const btn = (active: boolean) =>
    `p-1.5 rounded transition-colors ${
      active
        ? "bg-primary/15 text-primary"
        : "hover:bg-surface-container-high text-on-surface"
    }`;

  const guard = (fn: () => void) => (e: React.MouseEvent) => {
    e.preventDefault(); // keep editor focus
    fn();
  };

  return (
    <div className="overflow-hidden">
      <div className="flex items-center gap-1 p-2 border-b border-outline-variant/20 bg-surface-container-low shrink-0">
        <button type="button" className={btn(isBold)} aria-label="Bold" onMouseDown={guard(() => fmt("bold"))}>
          <IconFormatBold className="h-4 w-4" />
        </button>
        <button type="button" className={btn(isItalic)} aria-label="Italic" onMouseDown={guard(() => fmt("italic"))}>
          <IconFormatItalic className="h-4 w-4" />
        </button>
        <button type="button" className={btn(isUnderline)} aria-label="Underline" onMouseDown={guard(() => fmt("underline"))}>
          <IconFormatUnderlined className="h-4 w-4" />
        </button>
        <div className="w-px h-4 bg-outline-variant/40 mx-1" aria-hidden />
        <button type="button" className={btn(false)} aria-label="Bullet list" onMouseDown={guard(bulletList)}>
          <IconFormatListBulleted className="h-4 w-4" />
        </button>
        <button type="button" className={btn(false)} aria-label="Link" onMouseDown={guard(addLink)}>
          <IconLink className="h-4 w-4" />
        </button>
        <button type="button" className={btn(false)} aria-label="Insert image link" onMouseDown={guard(addImageLink)}>
          <IconImage className="h-4 w-4" />
        </button>
      </div>
      <div style={{ position: "relative", minHeight }}>
        <div
          ref={ref}
          contentEditable
          suppressContentEditableWarning
          role="textbox"
          aria-multiline
          aria-placeholder={placeholder}
          onInput={handleInput}
          onKeyUp={syncState}
          onMouseUp={syncState}
          style={{
            minHeight,
            padding: "8px 12px",
            fontSize: "12px",
            outline: "none",
            cursor: "text",
            wordBreak: "break-word",
            overflowWrap: "break-word",
          }}
        />
        {isEmpty && placeholder ? (
          <div
            aria-hidden
            style={{
              pointerEvents: "none",
              userSelect: "none",
              position: "absolute",
              top: 8,
              left: 12,
              fontSize: "12px",
              color: "rgba(var(--color-on-surface) / 0.4)",
            }}
          >
            {placeholder}
          </div>
        ) : null}
      </div>
    </div>
  );
}

export function MultiLangLexicalRichText({
  label,
  defaultValues,
  onValuesChange,
  placeholders,
  hint,
  autoFocus = false,
  className = "",
}: Props) {
  const { languages, resolvedIso, setActiveIso, values, currentValue, setValue } =
    useLangField(defaultValues, onValuesChange);

  const placeholder =
    placeholders?.[langKey(resolvedIso)] ?? placeholders?.[resolvedIso] ?? "";

  return (
    <div className="space-y-1.5">
      {label ? <label className={labelBase}>{label}</label> : null}
      <div className="relative">
        <LangSwitcher
          languages={languages}
          activeIso={resolvedIso}
          values={values}
          onSwitch={setActiveIso}
        />
        <div
          className={`border border-outline-variant/20 rounded-md overflow-hidden bg-surface ${
            languages.length > 0 ? "rounded-tr-none" : ""
          } ${className}`}
        >
          <NativeRichText
            key={resolvedIso}
            initialHtml={currentValue}
            placeholder={placeholder}
            autoFocus={autoFocus}
            onHtml={(html) => setValue(resolvedIso, html)}
          />
        </div>
      </div>
      {hint ? <p className="text-[10px] text-on-surface-variant">{hint}</p> : null}
    </div>
  );
}
