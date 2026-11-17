import { X } from "lucide-react";
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

/**
 * Ranges (the owner, 2026-11-17): a table "From level", "To level" and the value in its type's editor, sorted, the rows a
 * level reaches twice marked, a gap a faint row, × to remove a row and a last row adding one once its value is typed;
 * a text pasted, lines `1-49: value`, replaces the table, a line not read said by its number with nothing changed, and
 * "Copy as text" writes it back. Each change sends the whole ranges, built on the last ones sent.
 */
export function RangesTable({ variable, value, onChange, who, label }: RangesTableProps) {
  const { t, i18n } = useTranslation();
  const rows = rowsOf(value).sort((a, b) => a.from - b.from);
  const sent = useRef(rows);
  useEffect(() => {
    sent.current = rowsOf(value).sort((a, b) => a.from - b.from);
  }, [value]);
  const save = (change: (rows: Row[]) => Row[]) => {
    const next = change(sent.current).sort((a, b) => a.from - b.from);
    sent.current = next;
    return onChange(JSON.stringify(next));
  };
  const crossed = overlapping(rows);
  const one = { valueType: variable.valueType, list: variable.list, bounds: null };
  const [pasted, setPasted] = useState("");
  const [unread, setUnread] = useState<number | null>(null);
  const replace = async () => {
    const read = readRanges(pasted);
    if (!read.ok) {
      setUnread(read.line);
      return;
    }
    const values = await Promise.all(
      read.rows.map((row) => fromWords(variable.valueType, variable.list === true, row.text)),
    );
    const missing = values.indexOf(null);
    if (missing >= 0) {
      // The line of the row whose value names nothing, counting the blank lines skipped.
      const lines = pasted.split(/\r?\n/);
      const line = lines.findIndex((it) => it.includes(read.rows[missing]?.text ?? "\u0000")) + 1;
      setUnread(line);
      return;
    }
    setUnread(null);
    setPasted("");
    void save(() => read.rows.map((row, at) => ({ from: row.from, to: row.to, value: values[at] })));
  };
  const copy = async () => {
    const lines = await Promise.all(
      rows.map(async (row) => ({
        from: row.from,
        to: row.to,
        text: await toWords(variable.valueType, variable.list === true, row.value, i18n.language),
      })),
    );
    const text = writeRanges(lines);
    setPasted(text);
    await navigator.clipboard?.writeText(text).catch(() => undefined);
  };
  const level = (row: Row, at: number, end: "from" | "to") => (
    <input
      type="text"
      inputMode="numeric"
      aria-label={t(end === "from" ? "values.rangesFrom" : "values.rangesTo", { row: at + 1 })}
      aria-invalid={crossed.has(at)}
      defaultValue={row[end]}
      onBlur={(event) => {
        const typed = Number(event.target.value);
        const other = end === "from" ? row.to : row.from;
        const holds =
          Number.isInteger(typed) &&
          typed >= 1 &&
          typed <= TOP_LEVEL &&
          (end === "from" ? typed <= other : typed >= other);
        if (!holds || typed === row[end]) {
          event.target.value = String(row[end]);
          return;
        }
        void save((list) =>
          list.map((it) => (it.from === row.from && it.to === row.to ? { ...it, [end]: typed } : it)),
        );
      }}
      className={FIELD}
    />
  );
  const holes = gaps(rows);
  const last = rows.at(-1);
  return (
    <div className="flex flex-col gap-1 text-xs">
      {rows.map((row, at) => (
        <div key={`${row.from}-${row.to}`} className="flex items-center gap-1">
          {level(row, at, "from")}
          <span className="text-text-muted">–</span>
          {level(row, at, "to")}
          <div className="min-w-0 flex-1">
            <SettingEditor
              variable={one}
              value={row.value === undefined ? null : JSON.stringify(row.value)}
              onChange={(changed) =>
                save((list) =>
                  list.map((it) =>
                    it.from === row.from && it.to === row.to ? { ...it, value: JSON.parse(changed) } : it,
                  ),
                )
              }
              who={who}
              label={t("values.rangesValue", { label, row: at + 1 })}
            />
          </div>
          <button
            type="button"
            aria-label={t("values.rangesRemove", { row: at + 1 })}
            onClick={() => void save((list) => list.filter((it) => it.from !== row.from || it.to !== row.to))}
            className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
          >
            <X aria-hidden="true" size={14} strokeWidth={1.5} />
          </button>
        </div>
      ))}
      {crossed.size > 0 && <p className="text-[#f48771]">{t("values.rangesOverlap")}</p>}
      {holes.map(([from, to]) => (
        <p key={`gap-${from}`} className="text-text-faint">
          {t("values.rangesNothing", { from, to })}
        </p>
      ))}
      <div className="flex items-center gap-1">
        <span className="text-text-muted whitespace-nowrap">
          {t("values.rangesAdd", { from: (last?.to ?? 0) + 1, to: TOP_LEVEL })}
        </span>
        <div className="min-w-0 flex-1">
          <SettingEditor
            key={rows.length}
            variable={one}
            value={null}
            onChange={(added) => {
              const from = Math.min((sent.current.at(-1)?.to ?? 0) + 1, TOP_LEVEL);
              return save((list) => [...list, { from, to: TOP_LEVEL, value: JSON.parse(added) }]);
            }}
            who={who}
            label={t("values.rangesNew", { label })}
          />
        </div>
      </div>
      <textarea
        aria-label={t("values.rangesPaste", { label })}
        placeholder={t("values.rangesHint")}
        value={pasted}
        onChange={(event) => {
          setPasted(event.target.value);
          setUnread(null);
        }}
        rows={3}
        className={cn(
          "focus:border-focus min-h-[52px] rounded border border-[#3c3c3c] bg-[#181818] px-2 py-1 text-white outline-0",
          unread !== null && "border-[#f48771]",
        )}
      />
      {unread !== null && <p className="text-[#f48771]">{t("values.rangesUnread", { line: unread })}</p>}
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
