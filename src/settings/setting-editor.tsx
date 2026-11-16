import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import type { VariableView_Serialize as VariableView } from "@/bindings.ts";
import { Switch } from "@/components/ui/switch.tsx";
import { valueText } from "@/library/value-text.ts";
import { ZoneField, type ZoneValue } from "@/zone/zone-field.tsx";
import { JobSelect } from "./job-select.tsx";

/** The step of the roadmap whose actions use a bank and items, which brings their editors (the owner, 2026-11-16). */
const LATER = 5;

interface SettingEditorProps {
  variable: VariableView;
  /** The value shown as JSON: the character's own, else the default; `null` when neither is set. */
  value: string | null;
  /** A new value as JSON, which the back end checks against the setting's type and bounds. */
  onChange: (value: string) => void;
  /** Whose setting it is, as the map's banner says while choosing a Zone. */
  who: string;
  /** What the editor is named by, for assistive technology. */
  label: string;
}

/**
 * A setting's editor by its type (the owner, 2026-11-16): a Zone's field, a bounded number, a switch, a line of text, a
 * profession; a bank and items say their value only until step 5 brings their editors.
 */
export function SettingEditor({ variable, value, onChange, who, label }: SettingEditorProps) {
  const { t, i18n } = useTranslation();
  switch (variable.valueType) {
    case "zone":
      return <ZoneField value={zoneValue(value)} onChange={(zone) => onChange(JSON.stringify(zone))} who={who} />;
    case "percentage":
    case "number":
      return <NumberEditor variable={variable} value={value} onChange={onChange} label={label} />;
    case "bool":
      return (
        <Switch
          aria-label={label}
          checked={value === "true"}
          onCheckedChange={(checked) => onChange(JSON.stringify(checked))}
        />
      );
    case "text":
      return <TextEditor value={value} onChange={onChange} label={label} />;
    case "job":
      return <JobSelect value={jobValue(value)} onChange={(job) => onChange(JSON.stringify(job))} label={label} />;
    default:
      return (
        <p className="flex h-[26px] items-center gap-2 text-xs">
          <span className="truncate text-white">{valueText(variable.valueType, value, i18n.language)}</span>
          {(variable.valueType === "bank" || variable.valueType === "items") && (
            <span className="text-text-faint ml-auto text-[11px] whitespace-nowrap">
              {t("blueprint.fromStep", { step: LATER })}
            </span>
          )}
        </p>
      );
  }
}

/** A Zone's value read from its JSON; `null` when none is set or it is not of a Zone's shape. */
function zoneValue(json: string | null): ZoneValue | null {
  const value = parse(json);
  if (typeof value !== "object" || value === null) {
    return null;
  }
  if ("sub_area" in value && typeof value.sub_area === "number") {
    return { sub_area: value.sub_area };
  }
  return "zone" in value && typeof value.zone === "string" ? { zone: value.zone } : null;
}

function jobValue(json: string | null): number | null {
  const value = parse(json);
  return typeof value === "number" ? value : null;
}

function parse(json: string | null): unknown {
  if (json === null) {
    return null;
  }
  try {
    return JSON.parse(json);
  } catch {
    return null;
  }
}

const FIELD =
  "h-[26px] min-w-0 rounded border border-[#3c3c3c] bg-[#181818] px-2 text-xs text-white outline-0 focus:border-focus aria-invalid:border-[#f48771]";

/**
 * A whole number within the setting's bounds, sent when the field is left or Enter is pressed; one out of them or not
 * whole is marked and sent nowhere, and Escape or leaving puts the value back.
 */
function NumberEditor({ variable, value, onChange, label }: Omit<SettingEditorProps, "who">) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(value ?? "");
  useEffect(() => setDraft(value ?? ""), [value]);
  const [smallest, largest] = variable.bounds ?? [variable.valueType === "percentage" ? 0 : null, null];
  const number = Number(draft);
  const valid =
    draft.trim() !== "" &&
    Number.isInteger(number) &&
    (smallest === null || number >= smallest) &&
    (largest === null || number <= largest);
  const commit = () => {
    if (valid && String(number) !== value) {
      onChange(String(number));
    } else if (!valid) {
      setDraft(value ?? "");
    }
  };
  const bounds = smallest !== null && largest !== null ? t("settings.bounds", { smallest, largest }) : undefined;
  return (
    <span className="flex items-center gap-1.5 text-xs">
      <input
        type="text"
        inputMode="numeric"
        aria-label={label}
        aria-invalid={!valid}
        title={bounds}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={commit}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            commit();
          } else if (event.key === "Escape") {
            setDraft(value ?? "");
          }
        }}
        className={`${FIELD} w-[72px] text-right tabular-nums`}
      />
      {variable.valueType === "percentage" && <span className="text-text-muted">%</span>}
    </span>
  );
}

/** A line of text, sent when the field is left or Enter is pressed. */
function TextEditor({ value, onChange, label }: Omit<SettingEditorProps, "who" | "variable">) {
  const current = typeof parse(value) === "string" ? String(parse(value)) : "";
  const [draft, setDraft] = useState(current);
  useEffect(() => setDraft(current), [current]);
  const commit = () => {
    if (draft !== current) {
      onChange(JSON.stringify(draft));
    }
  };
  return (
    <input
      type="text"
      aria-label={label}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={commit}
      onKeyDown={(event) => event.key === "Enter" && commit()}
      className={`${FIELD} w-full`}
    />
  );
}
