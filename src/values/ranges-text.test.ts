import { describe, expect, it } from "vitest";

import { gaps, overlapping, readRanges, writeRanges } from "./ranges-text.ts";

describe("ranges as text", () => {
  it("reads lines `1-49: value` and a spreadsheet's columns, blank lines skipped", () => {
    const read = readRanges("1-49: Forêt d'Astrub\n\n50\t200\tChamps d'Astrub \r\n");
    expect(read).toEqual({
      ok: true,
      rows: [
        { from: 1, to: 49, text: "Forêt d'Astrub", line: 1 },
        { from: 50, to: 200, text: "Champs d'Astrub", line: 3 },
      ],
    });
  });

  it("says the first line it cannot read by its number", () => {
    expect(readRanges("1-49: a\nniveau 50: b")).toEqual({ ok: false, line: 2 });
    expect(readRanges("49-1: a")).toEqual({ ok: false, line: 1 }); // reversed
    expect(readRanges("0-49: a")).toEqual({ ok: false, line: 1 }); // below 1
    expect(readRanges("1-201: a")).toEqual({ ok: false, line: 1 }); // past 200
    expect(readRanges("1-49:   ")).toEqual({ ok: false, line: 1 }); // no value
  });

  it("writes rows that read back the same", () => {
    const rows = [
      { from: 1, to: 49, text: "1 min 30" },
      { from: 50, to: 200, text: "5,-18" },
    ];
    const read = readRanges(writeRanges(rows));
    expect(read.ok && read.rows.map(({ from, to, text }) => ({ from, to, text }))).toEqual(rows);
  });

  it("finds the rows a level reaches with another, and none for a gap", () => {
    expect([
      ...overlapping([
        { from: 1, to: 50 },
        { from: 50, to: 80 },
        { from: 90, to: 99 },
      ]),
    ]).toEqual([0, 1]);
    expect(
      overlapping([
        { from: 1, to: 9 },
        { from: 20, to: 30 },
      ]).size,
    ).toBe(0);
  });

  it("finds the levels no row reaches, up to 200", () => {
    expect(
      gaps([
        { from: 50, to: 59 },
        { from: 1, to: 9 },
      ]),
    ).toEqual([
      [10, 49],
      [60, 200],
    ]);
    expect(gaps([{ from: 1, to: 200 }])).toEqual([]);
    expect(gaps([])).toEqual([]);
  });
});
