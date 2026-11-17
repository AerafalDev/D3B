import { useRef, useState } from "react";

import { cn } from "@/lib/utils.ts";

interface InPlaceProps {
  /** The text shown, and edited. */
  value: string;
  /** What it is named by, for assistive technology and its hover. */
  label: string;
  /** Whether it may be left empty: a description may, a name not, and a blank one is put back. */
  blank: boolean;
  /** The text saved, without the spaces around it; whether it was. */
  onSave: (value: string) => Promise<boolean>;
  /** The look of the text, kept by the field. */
  className?: string;
  /** Shown when the text is empty, in place of nothing. */
  placeholder?: string;
}

/**
 * A text edited where it is shown (the owner's choice of 2026-11-17, as a zone's name): a click turns it into a field,
 * Enter or leaving saves it, Escape or a blank name puts it back.
 */
export function InPlace({ value, label, blank, onSave, className, placeholder }: InPlaceProps) {
  const [draft, setDraft] = useState<string | null>(null);
  // Escape ends it without saving, even when the field's removal blurs it after.
  const cancelled = useRef(false);
  if (draft === null) {
    return (
      <button
        type="button"
        title={label}
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
  return (
    <input
      // biome-ignore lint/a11y/noAutofocus: a click asked to type it now
      autoFocus
      aria-label={label}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      onFocus={(event) => event.currentTarget.select()}
      onBlur={save}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.currentTarget.blur();
        } else if (event.key === "Escape") {
          event.preventDefault();
          cancelled.current = true;
          event.currentTarget.blur();
        }
      }}
      className={cn(
        "focus:border-focus -mx-1 w-full min-w-0 rounded border border-[#3c3c3c] bg-[#181818] px-1 outline-0",
        className,
      )}
    />
  );
}
