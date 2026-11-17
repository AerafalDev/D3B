import { RotateCcw } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { cn } from "@/lib/utils.ts";
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
  /** For a wildcard its node notes no type for, the types it may take, the one it reads by default first. */
  open?: readonly string[];
  /**
   * A value as JSON, or none for the pin's default, saved at once (the owner, 2026-11-17), with the type an open
   * wildcard is given as; whether it was.
   */
  onValue: (value: string | null, of?: string) => Promise<boolean>;
}

/**
 * An input no wire feeds, in a blueprint of mine (the owner's choices of 2026-11-17): its label, its editor by its type,
 * as a setting's, and, when a value other than the pin's default is typed, a button putting the default back.
 */
export function InputRow({ pin, typed, type, who, open, onValue }: InputRowProps) {
  const { t, i18n } = useTranslation();
  // An open wildcard's value is typed as the type chosen beside it; the first is what it reads with none.
  const [chosen, setChosen] = useState(open?.[0] ?? type.valueType);
  const valueType = open === undefined ? type.valueType : chosen;
  // A wildcard reading its first type meanwhile needs none said for it; one reading nothing needs it always.
  const of = open !== undefined && (pin.carries === "wildcard" || chosen !== open[0]) ? chosen : undefined;
  const label = pinLabel(pin.id);
  const fallback = pin.default ?? null;
  const reset =
    // Both written by serde_json, so one value reads as one text.
    typed !== undefined && fallback !== null && typed !== fallback
      ? t("blueprint.details.reset", { value: valueText(valueType, fallback, i18n.language) })
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
      {open !== undefined && (
        <Select value={chosen} onValueChange={setChosen}>
          <SelectTrigger
            size="sm"
            aria-label={t("blueprint.details.typeOf", { pin: label })}
            className="h-[26px] w-full text-xs"
          >
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {open.map((one) => (
              <SelectItem key={one} value={one} className="text-xs">
                <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${one}`)} />
                {t(`blueprint.type.${one}` as never)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
      <SettingEditor
        // Another type chosen gives an editor of its own, with no draft of the one before.
        key={valueType}
        variable={{ ...type, valueType }}
        value={typed ?? fallback}
        // A text cleared is none, so a Prévenir with nothing to say keeps its problem.
        onChange={(value) => onValue(value === '""' ? null : value, of)}
        who={who}
        label={label}
      />
    </div>
  );
}
