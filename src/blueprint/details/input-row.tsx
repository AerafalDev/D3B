import { RotateCcw } from "lucide-react";
import { useTranslation } from "react-i18next";

import { valueText } from "@/library/value-text.ts";
import { SettingEditor, type Typed } from "@/settings/setting-editor.tsx";
import type { GraphNode } from "../node-sentence.ts";
import { pinLabel } from "../pin-label.ts";

interface InputRowProps {
  pin: GraphNode["pins"][number];
  /** The value typed there, as JSON, if one. */
  typed: string | undefined;
  /** Its type and, for a Set's value, its variable's bounds. */
  type: Typed;
  /** The node's sentence, for whom a Zone is chosen on the map. */
  who: string;
  /** A value as JSON, or none for the pin's default, saved at once (the owner, 2026-11-17); whether it was. */
  onValue: (value: string | null) => Promise<boolean>;
}

/**
 * An input no wire feeds, in a blueprint of mine (the owner's choices of 2026-11-17): its label, its editor by its type,
 * as a setting's, and, when a value other than the pin's default is typed, a button putting the default back.
 */
export function InputRow({ pin, typed, type, who, onValue }: InputRowProps) {
  const { t, i18n } = useTranslation();
  const label = pinLabel(pin.id);
  const fallback = pin.default ?? null;
  const reset =
    // Both written by serde_json, so one value reads as one text.
    typed !== undefined && fallback !== null && typed !== fallback
      ? t("blueprint.details.reset", { value: valueText(type.valueType, fallback, i18n.language) })
      : null;
  return (
    <div className="flex flex-col gap-1">
      <div className="flex h-[18px] items-center justify-between">
        <span className="text-text-muted">{label}</span>
        {reset !== null && (
          <button
            type="button"
            aria-label={reset}
            title={reset}
            onClick={() => void onValue(null)}
            className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 items-center justify-center rounded hover:text-white focus-visible:outline-1"
          >
            <RotateCcw aria-hidden="true" size={14} strokeWidth={1.5} />
          </button>
        )}
      </div>
      <SettingEditor variable={type} value={typed ?? fallback} onChange={onValue} who={who} label={label} />
    </div>
  );
}
