import { describe, expect, it } from "vitest";

import { seenAgo as raw } from "./played-store.ts";

// Intl puts no-break spaces between a number and its unit; the test reads them as spaces.
function seenAgo(then: number, now: number, language: string): string | null {
  return raw(then, now, language)?.replace(/[  ]/g, " ") ?? null;
}

const NOW = Date.UTC(2026, 10, 14, 12, 0, 0);
const HOUR = 3_600_000;

describe("seenAgo", () => {
  it("says how long ago, short, in the player's language", () => {
    expect(seenAgo(NOW - 2 * HOUR, NOW, "fr")).toBe("il y a 2 h");
    expect(seenAgo(NOW - 2 * HOUR, NOW, "en")).toBe("2 hr. ago");
    expect(seenAgo(NOW - 5 * 60_000, NOW, "fr")).toBe("il y a 5 min");
  });

  it("says yesterday, then days", () => {
    expect(seenAgo(NOW - 24 * HOUR, NOW, "fr")).toBe("hier");
    expect(seenAgo(NOW - 12 * 24 * HOUR, NOW, "fr")).toBe("il y a 12 j");
  });

  it("leaves under a minute to the list's own words", () => {
    expect(seenAgo(NOW - 30_000, NOW, "fr")).toBeNull();
    expect(seenAgo(NOW, NOW, "en")).toBeNull();
  });
});
