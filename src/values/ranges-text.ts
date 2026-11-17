/** A row of ranges as text: from a level to another, its value as the player types it. */
export interface RowText {
  from: number;
  to: number;
  text: string;
}

/** Ranges read from text, or the first line that does not read, numbered from 1. */
export type ReadRanges = { ok: true; rows: RowText[] } | { ok: false; line: number };

/** The highest level a range reaches. */
export const TOP_LEVEL = 200;

// `1-49: value`, or a spreadsheet's columns `1<tab>49<tab>value`.
const LINE = /^\s*(\d+)\s*-\s*(\d+)\s*:\s*(.+?)\s*$/;
const COLUMNS = /^\s*(\d+)\t(\d+)\t(.+?)\s*$/;

/**
 * The rows `text` holds (the owner, 2026-11-17): a line `1-49: value` or a spreadsheet's three columns, blank lines
 * skipped, levels from 1 to 200 with the first no higher than the second; any other line is said by its number.
 */
export function readRanges(text: string): ReadRanges {
  const rows: RowText[] = [];
  const lines = text.split(/\r?\n/);
  for (const [at, line] of lines.entries()) {
    if (line.trim() === "") {
      continue;
    }
    const match = LINE.exec(line) ?? COLUMNS.exec(line);
    const [from, to, value] = [Number(match?.[1]), Number(match?.[2]), match?.[3]];
    if (value === undefined || value.trim() === "" || from < 1 || to > TOP_LEVEL || from > to) {
      return { ok: false, line: at + 1 };
    }
    rows.push({ from, to, text: value.trim() });
  }
  return { ok: true, rows };
}

/** Rows as text, one `1-49: value` line each, which `readRanges` reads back the same. */
export function writeRanges(rows: readonly RowText[]): string {
  return rows.map((row) => `${row.from}-${row.to}: ${row.text}`).join("\n");
}

/** The indexes of the rows a level reaches with another, both ends included, as Rust's `Type::overlaps` decides. */
export function overlapping(rows: readonly { from: number; to: number }[]): Set<number> {
  const found = new Set<number>();
  for (const [at, row] of rows.entries()) {
    for (const [other, next] of rows.entries()) {
      if (at !== other && row.from <= next.to && next.from <= row.to) {
        found.add(at);
      }
    }
  }
  return found;
}

/** The levels from 1 to 200 no row reaches, as `[from, to]` spans, in order. */
export function gaps(rows: readonly { from: number; to: number }[]): [number, number][] {
  const sorted = [...rows].sort((a, b) => a.from - b.from);
  const found: [number, number][] = [];
  let next = 1;
  for (const row of sorted) {
    if (row.from > next) {
      found.push([next, row.from - 1]);
    }
    next = Math.max(next, row.to + 1);
  }
  if (next <= TOP_LEVEL && sorted.length > 0) {
    found.push([next, TOP_LEVEL]);
  }
  return found;
}
