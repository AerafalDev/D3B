import { beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { valueText } from "./value-text.ts";

describe("a value as the interface reads it", () => {
  beforeAll(() => {
    initI18n();
  });

  it("reads a duration's seconds as minutes and seconds", () => {
    const read = (seconds: string) => valueText("duration", seconds, "fr");
    expect([read("300"), read("90"), read("45"), read("0")]).toEqual(["5 min", "1 min 30", "45 s", "0 s"]);
  });

  it("keeps a duration that is no whole number of seconds as it is written", () => {
    expect([valueText("duration", "-5", "fr"), valueText("duration", '"5 min"', "fr")]).toEqual(["-5", '"5 min"']);
  });
});
