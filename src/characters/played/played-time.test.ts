import { act, renderHook } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { seenAgo as rawAgo, seenOn as rawOn, useNow } from "./played-time.ts";

// Intl puts no-break spaces between a number and its unit; the tests read them as spaces.
const plain = (text: string) => text.replace(/[  ]/g, " ");
const seenAgo = (then: number, now: number, language: string) => {
  const said = rawAgo(then, now, language);
  return said === null ? null : plain(said);
};
const seenOn = (then: number, now: number, language: string) => plain(rawOn(then, now, language));

const NOW = new Date(2026, 10, 14, 12, 0, 0).getTime();
const MINUTE = 60_000;
const HOUR = 60 * MINUTE;

describe("seenAgo", () => {
  it("says minutes and hours short, as the mock-up does", () => {
    expect(seenAgo(NOW - 5 * MINUTE, NOW, "fr")).toBe("il y a 5 min");
    expect(seenAgo(NOW - 2 * HOUR, NOW, "fr")).toBe("il y a 2 h");
    expect(seenAgo(NOW - 2 * HOUR, NOW, "en")).toBe("2 hr. ago");
  });

  it("says yesterday, then days in full", () => {
    expect(seenAgo(NOW - 24 * HOUR, NOW, "fr")).toBe("hier");
    expect(seenAgo(NOW - 12 * 24 * HOUR, NOW, "fr")).toBe("il y a 12 jours");
  });

  it("chooses the unit once rounded, at each boundary", () => {
    expect(seenAgo(NOW - (59 * MINUTE + 36_000), NOW, "fr")).toBe("il y a 1 h");
    expect(seenAgo(NOW - (23 * HOUR + 36 * MINUTE), NOW, "fr")).toBe("hier");
  });

  it("leaves under a minute, and a time ahead of now, to the list's own words", () => {
    expect(seenAgo(NOW - 29_000, NOW, "fr")).toBeNull();
    expect(seenAgo(NOW, NOW, "en")).toBeNull();
    expect(seenAgo(NOW + 2 * HOUR, NOW, "en")).toBeNull();
  });
});

describe("seenOn", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  it("says today, yesterday, or the date, with the time", () => {
    expect(seenOn(NOW - 20 * MINUTE, NOW, "fr")).toBe("aujourd'hui à 11:40");
    expect(seenOn(NOW - 24 * HOUR, NOW, "fr")).toBe("hier à 12:00");
    expect(seenOn(NOW - 12 * 24 * HOUR, NOW, "fr")).toBe("le 2 nov. 2026 à 12:00");
  });
});

describe("useNow", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("reads the clock again each minute", () => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    const { result } = renderHook(() => useNow());
    expect(result.current).toBe(NOW);
    act(() => {
      vi.advanceTimersByTime(MINUTE);
    });
    expect(result.current).toBe(NOW + MINUTE);
  });
});
