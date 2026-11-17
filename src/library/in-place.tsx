import { useEffect, useRef, useState } from "react";

import { cn } from "@/lib/utils.ts";

interface InPlaceProps {
  /** The text shown, and edited. */
  value: string;
  /** What it is named by, for assistive technology, and its hover while empty. */
  label: string;
  /** Whether it may be left empty: a description may, a name not, and a blank one is put back. */
  blank: boolean;
  /** A text of several lines (a description), whose line breaks Shift+Enter types and the field keeps. */
  multiline?: boolean;
  /** The text saved, without the spaces around it; whether it was. */
  onSave: (value: string) => Promise<boolean>;
  /** The look of the text, kept by the field. */
  className?: string;
  /** Shown when the text is empty, in place of nothing. */
  placeholder?: string;
}

/**
 * A text edited where it is shown (the owner's choice of 2026-11-17, as a zone's name): a click turns it into a field,
 * Enter or leaving saves it, Escape or a blank name puts it back, and the text takes the focus back, so the tab's keys
 * (Ctrl+Z) act at once.
 */
export function InPlace({ value, label, blank, multiline = false, onSave, className, placeholder }: InPlaceProps) {
  const [draft, setDraft] = useState<string | null>(null);
  // Escape ends it without saving, even when the field's removal blurs it after.
  const cancelled = useRef(false);
  const shown = useRef<HTMLButtonElement>(null);
  // Whether the text takes the focus back once the field is gone: when Enter or Escape ended it, not a click elsewhere.
  const refocus = useRef(false);
  useEffect(() => {
    if (draft === null && refocus.current) {
      refocus.current = false;
      shown.current?.focus({ preventScroll: true });
    }
  }, [draft]);

  if (draft === null) {
    return (
      <button
        ref={shown}
        type="button"
        // The whole text in the hover, as it may be cut.
        title={value === "" ? label : value}
        onClick={() => {
          cancelled.current = false;
          setDraft(value);
        }}
        className={cn(
          "hover:bg-hover focus-visible:outline-focus -mx-1 max-w-full truncate rounded px-1 text-left focus-visible:outline-1",
          value === "" && "italic opacity-60",
          className,
        )}
      >
        {value === "" ? placeholder : value}
      </button>
    );
  }
  const save = () => {
    setDraft(null);
    const trimmed = draft.trim();
    if (cancelled.current || (!blank && trimmed === "") || trimmed === value) {
      return;
    }
    void onSave(trimmed);
  };
  const field = {
    // biome-ignore lint/a11y/noAutofocus: a click asked to type it now
    autoFocus: true,
    "aria-label": label,
    value: draft,
    onFocus: (event: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => event.currentTarget.select(),
    onBlur: save,
    onKeyDown: (event: React.KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        refocus.current = true;
        event.currentTarget.blur();
      } else if (event.key === "Escape") {
        event.preventDefault();
        cancelled.current = true;
        refocus.current = true;
        event.currentTarget.blur();
      }
    },
    className: cn(
      "focus:border-focus -mx-1 w-full min-w-0 rounded border border-[#3c3c3c] bg-[#181818] px-1 outline-0",
      className,
    ),
  };
  return multiline ? (
    <textarea {...field} rows={2} onChange={(event) => setDraft(event.target.value)} />
  ) : (
    <input {...field} onChange={(event) => setDraft(event.target.value)} />
  );
}
