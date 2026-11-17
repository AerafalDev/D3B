import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "./i18n.ts";
import { nodeWord } from "./node-words.ts";

describe("a word of the nodes' namespace", () => {
  beforeAll(() => {
    initI18n();
  });
  afterEach(async () => {
    await i18next.changeLanguage("fr");
  });

  it("is said in the player's language", async () => {
    await i18next.changeLanguage("fr");
    expect(nodeWord("kinds.finish.sentence")).toBe("Terminer");
    await i18next.changeLanguage("en");
    expect(nodeWord("kinds.finish.sentence")).toBe("Finish");
  });

  it("falls back to English when the player's language lacks it, and is none when neither has it", async () => {
    await i18next.changeLanguage("fr");
    i18next.addResource("en", "nodes", "kinds.teleport.sentence", "Teleport");
    expect(nodeWord("kinds.teleport.sentence")).toBe("Teleport");
    expect(nodeWord("kinds.nowhere.sentence")).toBeNull();
    expect(nodeWord("pins.run"), "an empty label is one").toBe("");
  });
});
