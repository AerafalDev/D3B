import i18next from "i18next";
import { beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { shapedText, valueText } from "./value-text.ts";

describe("a value as the interface reads it", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  it("reads a duration's seconds as minutes and seconds, in the player's language", async () => {
    const read = (seconds: string) => valueText("duration", seconds, i18next.language);
    expect([read("300"), read("90"), read("45"), read("0")]).toEqual(["5 min", "1 min 30", "45 s", "0 s"]);
    await i18next.changeLanguage("en");
    expect(read("90")).toBe("1 min 30 s");
    await i18next.changeLanguage("fr");
  });

  it("says a list and ranges by how many they hold, and a map, an item and an NPC by their id until their data", () => {
    expect(shapedText({ valueType: "zone", list: true }, "[1, 2]", "fr")).toBe("2 valeurs");
    expect(shapedText({ valueType: "zone", ranges: true }, '[{"from": 1, "to": 9, "value": 1}]', "fr")).toBe(
      "1 tranche",
    );
    expect(shapedText({ valueType: "time" }, '"23:00"', "fr")).toBe("23:00");
    expect([valueText("map", "1", "fr"), valueText("item", "289", "fr"), valueText("npc", "774", "fr")]).toEqual([
      "Carte 1",
      "Objet 289",
      "PNJ 774",
    ]);
  });

  it("keeps a duration that is no whole number of seconds as it is written", () => {
    expect([valueText("duration", "-5", "fr"), valueText("duration", '"5 min"', "fr")]).toEqual(["-5", '"5 min"']);
  });
});
