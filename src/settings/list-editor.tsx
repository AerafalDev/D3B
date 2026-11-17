import { Plus, X } from "lucide-react";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";

import { SettingEditor, type Typed } from "./setting-editor.tsx";

interface ListEditorProps {
  /** The list's type, its values each of `valueType`. */
  variable: Typed;
  /** The list as JSON, `null` when none is set. */
  value: string | null;
  /** The whole list, as JSON, saved at once; whether it was kept. */
  onChange: (value: string) => Promise<boolean>;
  who: string;
  label: string;
}

/** The values a list's JSON holds, each as JSON; none for a value that is no list. */
function rows(json: string | null): string[] {
  if (json === null) {
    return [];
  }
  try {
    const value: unknown = JSON.parse(json);
    return Array.isArray(value) ? value.map((one) => JSON.stringify(one)) : [];
  } catch {
    return [];
  }
}

/** The types a value is typed as in Details yet; the others' editors come with their data (v2.2's next PRs). */
const EDITED = ["bool", "number", "percentage", "text", "zone", "job", "duration", "time", "item", "map"];

/**
 * A list (the owner, 2026-11-17: v2.2's, so my variables take one now): a row per value in its type's own editor, × to
 * remove it, and a last row (the `+` only marks it) to add one, which joins the list once a value is typed there. Each
 * change sends the whole list, which the back end checks against the type, built on the last one sent, so a field left
 * by a click on × is not lost before the list comes back.
 */
export function ListEditor({ variable, value, onChange, who, label }: ListEditorProps) {
  const { t } = useTranslation();
  const values = rows(value);
  const sent = useRef(values);
  useEffect(() => {
    sent.current = rows(value);
  }, [value]);
  const one = { valueType: variable.valueType, bounds: null };
  const save = (change: (list: string[]) => string[]) => {
    const next = change(sent.current);
    sent.current = next;
    return onChange(`[${next.join(",")}]`).then((kept) => {
      if (!kept) {
        sent.current = rows(value);
      }
      return kept;
    });
  };
  return (
    <div className="flex flex-col gap-1">
      {values.map((row, at) => (
        // A row is its place in the list: a value changed keeps it, a row removed moves those after it up.
        // biome-ignore lint/suspicious/noArrayIndexKey: a list's values have no identity but their place
        <div key={at} className="flex items-center gap-1">
          <div className="min-w-0 flex-1">
            <SettingEditor
              variable={one}
              value={row}
              onChange={(changed) => save((list) => list.map((it, index) => (index === at ? changed : it)))}
              who={who}
              label={t("values.row", { label, row: at + 1 })}
            />
          </div>
          <button
            type="button"
            aria-label={t("values.remove", { row: at + 1 })}
            onClick={() => void save((list) => list.filter((_, index) => index !== at))}
            className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
          >
            <X aria-hidden="true" size={14} strokeWidth={1.5} />
          </button>
        </div>
      ))}
      {EDITED.includes(variable.valueType) && (
        <div className="flex items-center gap-1">
          <Plus aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted shrink-0" />
          <div className="min-w-0 flex-1">
            <SettingEditor
              // A new row each time one joins, so its field starts empty again.
              key={values.length}
              variable={one}
              value={null}
              onChange={(added) => save((list) => [...list, added])}
              who={who}
              label={t("values.add", { label })}
            />
          </div>
        </div>
      )}
    </div>
  );
}
