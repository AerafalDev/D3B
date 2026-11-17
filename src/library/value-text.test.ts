import i18next from "i18next";
import { beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { valueText } from "./value-text.ts";

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

  it("keeps a duration that is no whole number of seconds as it is written", () => {
    expect([valueText("duration", "-5", "fr"), valueText("duration", '"5 min"', "fr")]).toEqual(["-5", '"5 min"']);
  });
});
