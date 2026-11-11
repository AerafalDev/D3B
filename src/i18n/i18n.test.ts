import { describe, expect, it } from "vitest";

import { resources, systemLanguage } from "./i18n.ts";

function keys(value: unknown, prefix = ""): string[] {
  if (typeof value !== "object" || value === null) {
    return [prefix];
  }
  return Object.entries(value).flatMap(([key, child]) => keys(child, prefix ? `${prefix}.${key}` : key));
}

describe("locales", () => {
  it("give French every key English has, and no other", () => {
    expect(keys(resources.fr.translation).sort()).toEqual(keys(resources.en.translation).sort());
  });
});

describe("systemLanguage", () => {
  it("takes the first language the app speaks", () => {
    expect(systemLanguage(["de-DE", "fr-FR", "en-US"])).toBe("fr");
  });

  it("falls back to English", () => {
    expect(systemLanguage(["de-DE"])).toBe("en");
    expect(systemLanguage([])).toBe("en");
  });
});
