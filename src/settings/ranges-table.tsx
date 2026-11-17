import { Plus, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { gaps, overlapping, readRanges, TOP_LEVEL, writeRanges } from "@/values/ranges-text.ts";
import { fromWords, toWords } from "@/values/value-words.ts";
import { SettingEditor, type Typed } from "./setting-editor.tsx";

/** A row of ranges as the file holds it. */
interface Row {
  from: number;
  to: number;
  value: unknown;
}

interface RangesTableProps {
  /** The ranges' type, its rows' values each of `valueType`, or lists of them with `list`. */
  variable: Typed;
  /** The ranges as JSON, `null` when none are set. */
  value: string | null;
  /** The whole ranges, as JSON, saved at once; whether they were kept. */
  onChange: (value: string) => Promise<boolean>;
  who: string;
  label: string;
}

function rowsOf(json: string | null): Row[] {
  if (json === null) {
    return [];
  }
  try {
    const value: unknown = JSON.parse(json);
    if (!Array.isArray(value)) {
      return [];
    }
    return value.flatMap((row: unknown) => {
      const one = row as Partial<Row> | null;
      return typeof one?.from === "number" && typeof one.to === "number"
        ? [{ from: one.from, to: one.to, value: one.value }]
        : [];
    });
  } catch {
    return [];
  }
}

const FIELD =
  "h-[26px] w-[44px] rounded border border-[#3c3c3c] bg-[#181818] px-1 text-right text-xs text-white tabular-nums outline-0 focus:border-focus aria-invalid:border-[#f48771]";

interface LevelProps {
  level: number;
  label: string;
  crossed: boolean;
  /** Whether a level typed holds: 1 to 200, and on the right side of the row's other end. */
  holds: (level: number) => boolean;
  onLevel: (level: number) => void;
}

/** A row's level, kept in a draft while typed, sent when left, put back when it does not hold. */
function Level({ level, label, crossed, holds, onLevel }: LevelProps) {
  const [draft, setDraft] = useState(String(level));
  useEffect(() => setDraft(String(level)), [level]);
  const commit = () => {
    const typed = Number(draft);
    if (!Number.isInteger(typed) || !holds(typed) || typed === level) {
      setDraft(String(level));
      return;
    }
    onLevel(typed);
  };
  return (
    <input
      type="text"
      inputMode="numeric"
      aria-label={label}
      aria-invalid={crossed}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={commit}
      onKeyDown={(event) => event.key === "Enter" && commit()}
      className={FIELD}
    />
  );
}

/**
 * Ranges (the owner, 2026-11-17): a table "From level", "To level" and the value in its type's editor, sorted, the rows a
 * level reaches twice marked, a gap a faint row in its place, × to remove a row and a last row adding one from the
 * level after the highest once its value is typed; a text pasted, lines `1-49: value`, replaces the table, a line not
 * read said by its number with nothing changed, and "Copy as text" writes it back. Each change sends the whole ranges,
 * built on the last ones sent, a row known by its place in them, so a level changed then × clicked removes that row.
 */
export function RangesTable({ variable, value, onChange, who, label }: RangesTableProps) {
  const { t, i18n } = useTranslation();
  const rows = rowsOf(value);
  const sent = useRef(rows);
  useEffect(() => {
    sent.current = rowsOf(value);
  }, [value]);
  const save = (change: (rows: Row[]) => Row[]) => {
    const next = change(sent.current);
    sent.current = next;
    return onChange(JSON.stringify(next)).then((kept) => {
      if (!kept) {
        sent.current = rowsOf(value);
      }
      return kept;
    });
  };
  // Shown sorted, each row known by its place in the ranges sent, which a level changed keeps.
  const order = rows.map((_, at) => at).sort((a, b) => (rows[a]?.from ?? 0) - (rows[b]?.from ?? 0));
  const crossed = overlapping(rows);
  const holes = gaps(rows);
  const one = { valueType: variable.valueType, list: variable.list, bounds: null };
  const highest = Math.max(0, ...rows.map((row) => row.to));
  const [pasted, setPasted] = useState("");
  const [said, setSaid] = useState<string | null>(null);
  const replace = async () => {
    if (pasted.trim() === "") {
      return;
    }
    const read = readRanges(pasted);
    if (!read.ok) {
      setSaid(t("values.rangesUnread", { line: read.line }));
      return;
    }
    const values = await Promise.all(
      read.rows.map((row) => fromWords(variable.valueType, variable.list === true, row.text)),
    );
    const missing = values.indexOf(null);
    if (missing >= 0) {
      setSaid(t("values.rangesUnread", { line: read.rows[missing]?.line ?? 0 }));
      return;
    }
    const kept = await save(() => read.rows.map((row, at) => ({ from: row.from, to: row.to, value: values[at] })));
    setSaid(kept ? null : t("values.rangesRefused"));
    if (kept) {
      setPasted("");
    }
  };
  const copy = async () => {
    const lines = await Promise.all(
      order.map(async (at) => {
        const row = rows[at] as Row;
        const text = await toWords(variable.valueType, variable.list === true, row.value, i18n.language);
        return { from: row.from, to: row.to, text };
      }),
    );
    const text = writeRanges(lines);
    setPasted(text);
    setSaid(null);
    await navigator.clipboard?.writeText(text).catch(() => undefined);
  };
  const gapBefore = (from: number) => holes.find(([, to]) => to === from - 1);
  return (
    <div className="flex flex-col gap-1 text-xs">
      {rows.length > 0 && (
        <div className="text-text-muted flex gap-1" aria-hidden="true">
          <span className="w-[44px] text-right">{t("values.rangesFromHead")}</span>
          <span className="w-[44px] pl-3 text-right">{t("values.rangesToHead")}</span>
        </div>
      )}
      {order.map((at, shown) => {
        const row = rows[at] as Row;
        const gap = gapBefore(row.from);
        return (
          <div key={at} className="flex flex-col gap-1">
            {gap !== undefined && (
              <p className="text-text-faint">{t("values.rangesNothing", { from: gap[0], to: gap[1] })}</p>
            )}
            <div className="flex items-center gap-1">
              <Level
                level={row.from}
                label={t("values.rangesFrom", { row: shown + 1 })}
                crossed={crossed.has(at)}
                holds={(level) => level >= 1 && level <= row.to}
                onLevel={(from) => void save((list) => list.map((it, index) => (index === at ? { ...it, from } : it)))}
              />
              <span className="text-text-muted">–</span>
              <Level
                level={row.to}
                label={t("values.rangesTo", { row: shown + 1 })}
                crossed={crossed.has(at)}
                holds={(level) => level <= TOP_LEVEL && level >= row.from}
                onLevel={(to) => void save((list) => list.map((it, index) => (index === at ? { ...it, to } : it)))}
              />
              <div className="min-w-0 flex-1">
                <SettingEditor
                  variable={one}
                  value={row.value === undefined ? null : JSON.stringify(row.value)}
                  onChange={(changed) =>
                    save((list) =>
                      list.map((it, index) => (index === at ? { ...it, value: JSON.parse(changed) as unknown } : it)),
                    )
                  }
                  who={who}
                  label={t("values.rangesValue", { label, row: shown + 1 })}
                />
              </div>
              <button
                type="button"
                aria-label={t("values.rangesRemove", { row: shown + 1 })}
                onClick={() => void save((list) => list.filter((_, index) => index !== at))}
                className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
              >
                <X aria-hidden="true" size={14} strokeWidth={1.5} />
              </button>
            </div>
          </div>
        );
      })}
      {holes
        .filter(([from]) => from > highest)
        .map(([from, to]) => (
          <p key={`gap-${from}`} className="text-text-faint">
            {t("values.rangesNothing", { from, to })}
          </p>
        ))}
      {crossed.size > 0 && <p className="text-[#f48771]">{t("values.rangesOverlap")}</p>}
      {highest < TOP_LEVEL && (
        <div className="flex items-center gap-1">
          <Plus aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted shrink-0" />
          <span className="text-text-muted whitespace-nowrap">
            {t("values.rangesAdd", { from: highest + 1, to: TOP_LEVEL })}
          </span>
          <div className="min-w-0 flex-1">
            <SettingEditor
              key={rows.length}
              variable={one}
              value={null}
              onChange={(added) => {
                const from = Math.max(0, ...sent.current.map((row) => row.to)) + 1;
                return save((list) => [...list, { from, to: TOP_LEVEL, value: JSON.parse(added) as unknown }]);
              }}
              who={who}
              label={t("values.rangesNew", { label })}
            />
          </div>
        </div>
      )}
      <textarea
        aria-label={t("values.rangesPaste", { label })}
        placeholder={t("values.rangesHint")}
        value={pasted}
        onChange={(event) => {
          setPasted(event.target.value);
          setSaid(null);
        }}
        rows={3}
        className={cn(
          "focus:border-focus min-h-[52px] rounded border border-[#3c3c3c] bg-[#181818] px-2 py-1 text-white outline-0",
          said !== null && "border-[#f48771]",
        )}
      />
      {said !== null && <p className="text-[#f48771]">{said}</p>}
      <div className="flex gap-1.5">
        <button
          type="button"
          onClick={() => void replace()}
          className="hover:bg-hover focus-visible:outline-focus rounded border border-[#3c3c3c] px-2 py-0.5 focus-visible:outline-1"
        >
          {t("values.rangesReplace")}
        </button>
        <button
          type="button"
          onClick={() => void copy()}
          className="hover:bg-hover focus-visible:outline-focus rounded border border-[#3c3c3c] px-2 py-0.5 focus-visible:outline-1"
        >
          {t("values.rangesCopy")}
        </button>
      </div>
    </div>
  );
}
