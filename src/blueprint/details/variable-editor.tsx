import { Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import type { VariableChange } from "@/bindings.ts";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { Switch } from "@/components/ui/switch.tsx";
import { cn } from "@/lib/utils.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { SettingEditor } from "@/settings/setting-editor.tsx";
import { DetailRow } from "./detail-row.tsx";

type Variable = BlueprintSummary["variables"][number];

/** The types a variable of mine takes, in the order the picker of types lists them. */
const TYPES = ["bool", "number", "percentage", "text", "zone", "route", "bank", "items", "job"] as const;

interface VariableEditorProps {
  variable: Variable;
  /** How many nodes read or write it, in every graph. */
  usedBy: number;
  /** The variable made `change`, saved at once (the owner, 2026-11-17); whether it was. */
  onChange: (change: VariableChange) => Promise<boolean>;
  /** Deletes it with its nodes, undone by Ctrl+Z. */
  onDelete: () => void;
}

const FIELD =
  "h-[26px] min-w-0 rounded border border-[#3c3c3c] bg-[#181818] px-2 text-xs text-white outline-0 focus:border-focus aria-invalid:border-[#f48771]";

/** What `variable` becomes when one field changes. */
function changed(variable: Variable, fields: Partial<VariableChange>): VariableChange {
  return {
    id: variable.id,
    name: variable.name,
    valueType: variable.valueType,
    default: variable.default,
    bounds: variable.bounds,
    perCharacter: variable.perCharacter,
    memory: variable.memory,
    help: variable.help,
    ...fields,
  };
}

/**
 * A variable of mine in Details (step 3's (14), the owner's choices of 2026-11-17): its name, type, default, bounds,
 * whether each character sets it, its help, how long it lasts, the nodes that use it, and Delete; each field saved
 * when it is left, a new type dropping what it no longer fits.
 */
export function VariableEditor({ variable, usedBy, onChange, onDelete }: VariableEditorProps) {
  const { t } = useTranslation();
  const save = (fields: Partial<VariableChange>) => onChange(changed(variable, fields));
  const numeric = variable.valueType === "number" || variable.valueType === "percentage";
  return (
    <>
      <TextField
        label={t("blueprint.details.name")}
        value={variable.name}
        blank={false}
        onCommit={(name) => save({ name })}
        className="text-sm"
      />
      <DetailRow label={t("blueprint.details.type")}>
        <Select value={variable.valueType} onValueChange={(valueType) => void save({ valueType })}>
          <SelectTrigger size="sm" aria-label={t("blueprint.details.type")} className="h-[26px] w-full text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TYPES.map((type) => (
              <SelectItem key={type} value={type} className="text-xs">
                <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${type}`)} />
                {t(`blueprint.type.${type}`)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </DetailRow>
      <DetailRow label={t("blueprint.details.default")}>
        <SettingEditor
          variable={variable}
          value={variable.default}
          onChange={(value) => save({ default: value })}
          who={variable.name}
          label={t("blueprint.details.default")}
        />
      </DetailRow>
      {numeric && (
        <DetailRow label={t("blueprint.details.bounds")}>
          <Bounds
            bounds={variable.bounds}
            percentage={variable.valueType === "percentage"}
            onCommit={(bounds) => save({ bounds })}
          />
        </DetailRow>
      )}
      <Toggle
        label={t("blueprint.details.perCharacter")}
        checked={variable.perCharacter}
        onChange={(perCharacter) => void save({ perCharacter })}
      />
      <TextField label={t("blueprint.details.help")} value={variable.help} blank onCommit={(help) => save({ help })} />
      <Toggle
        label={t("blueprint.details.memoryToggle")}
        checked={variable.memory}
        onChange={(memory) => void save({ memory })}
      />
      <DetailRow label={t("blueprint.details.usedBy")}>{t("blueprint.details.nodes", { count: usedBy })}</DetailRow>
      <button
        type="button"
        onClick={onDelete}
        className="hover:bg-hover focus-visible:outline-focus flex items-center gap-1.5 self-start rounded border border-[#3c3c3c] px-3 py-1 text-xs focus-visible:outline-1"
      >
        <Trash2 aria-hidden="true" size={14} strokeWidth={1.5} />
        {t("blueprint.details.deleteVariable")}
      </button>
    </>
  );
}

interface TextFieldProps {
  label: string;
  value: string;
  /** Whether it may be left empty; a blank name is put back. */
  blank: boolean;
  onCommit: (value: string) => Promise<boolean>;
  className?: string;
}

/** A line saved when Enter or leaving ends it, put back by Escape or a refusal. */
function TextField({ label, value, blank, onCommit, className }: TextFieldProps) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  const commit = () => {
    const trimmed = draft.trim();
    if ((!blank && trimmed === "") || trimmed === value) {
      setDraft(value);
    } else {
      void onCommit(trimmed).then((kept) => kept || setDraft(value));
    }
  };
  return (
    <DetailRow label={label}>
      <input
        type="text"
        aria-label={label}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={commit}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            event.currentTarget.blur();
          } else if (event.key === "Escape") {
            setDraft(value);
          }
        }}
        className={cn(FIELD, "w-full", className)}
      />
    </DetailRow>
  );
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (on: boolean) => void }) {
  return (
    <div className="flex items-center justify-between gap-2">
      <span className="text-text-muted">{label}</span>
      <Switch aria-label={label} checked={checked} onCheckedChange={onChange} />
    </div>
  );
}

interface BoundsProps {
  bounds: [number, number] | null;
  /** A percentage's bounds stay within 0 to 100. */
  percentage: boolean;
  onCommit: (bounds: [number, number] | null) => Promise<boolean>;
}

/** The largest whole number the file's bounds hold either way. */
const LIMIT = 2 ** 31 - 1;

/**
 * The smallest and largest value, two whole numbers saved together when the focus leaves both, so the second can be
 * typed after the first; both left empty, none. A pair the wrong way round, a percentage's past 0 to 100, or a refusal
 * puts the bounds saved back.
 */
function Bounds({ bounds, percentage, onCommit }: BoundsProps) {
  const { t } = useTranslation();
  const shown = (at: 0 | 1) => (bounds === null ? "" : String(bounds[at]));
  const [low, setLow] = useState(shown(0));
  const [high, setHigh] = useState(shown(1));
  const putBack = () => {
    setLow(shown(0));
    setHigh(shown(1));
  };
  // biome-ignore lint/correctness/useExhaustiveDependencies: the fields follow the bounds saved
  useEffect(putBack, [bounds?.[0], bounds?.[1]]);
  const whole = (text: string) => {
    const number = Number(text);
    return text.trim() !== "" && Number.isInteger(number) && Math.abs(number) <= LIMIT ? number : null;
  };
  const none = low.trim() === "" && high.trim() === "";
  const smallest = whole(low);
  const largest = whole(high);
  const pair: [number, number] | null = smallest !== null && largest !== null ? [smallest, largest] : null;
  const valid = none || (pair !== null && pair[0] <= pair[1] && (!percentage || (pair[0] >= 0 && pair[1] <= 100)));
  const commit = () => {
    const next = none ? null : pair;
    if (!valid) {
      putBack();
    } else if (JSON.stringify(next) !== JSON.stringify(bounds)) {
      void onCommit(next).then((kept) => kept || putBack());
    }
  };
  const field = (label: string, value: string, set: (text: string) => void) => (
    <input
      type="text"
      inputMode="numeric"
      aria-label={label}
      aria-invalid={!valid}
      value={value}
      onChange={(event) => set(event.target.value)}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          commit();
        } else if (event.key === "Escape") {
          putBack();
        }
      }}
      className={cn(FIELD, "w-[64px] text-right tabular-nums")}
    />
  );
  return (
    <fieldset
      aria-label={t("blueprint.details.bounds")}
      className="m-0 flex items-center gap-1.5 border-0 p-0"
      onBlur={(event) => {
        // Saved once the focus leaves the pair, not when it goes from one field to the other.
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          commit();
        }
      }}
    >
      {field(t("blueprint.details.smallest"), low, setLow)}
      <span className="text-text-muted">–</span>
      {field(t("blueprint.details.largest"), high, setHigh)}
    </fieldset>
  );
}
