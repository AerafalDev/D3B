import { describe, expect, it } from "vitest";

import { parseDuration } from "./duration.ts";

describe("a duration typed", () => {
  it("reads minutes, seconds or both", () => {
    const read = ["5 min", "1 min 30", "1 min 30 s", "90 s", "0 s", " 2MIN ", "10min5s"].map(parseDuration);
    expect(read).toEqual([300, 90, 90, 90, 0, 120, 605]);
  });

  it("refuses a bare number, seconds past a minute after minutes, and anything else", () => {
    const refused = ["30", "", "1 min 60", "-5 min", "1.5 min", "5 minutes", "1 h"].map(parseDuration);
    expect(refused).toEqual([null, null, null, null, null, null, null]);
  });
});
