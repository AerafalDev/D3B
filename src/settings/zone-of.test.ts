import { describe, expect, it } from "vitest";

import type { BlueprintSummary_Serialize, PlayedCharacter_Serialize } from "@/bindings.ts";
import { zoneOf } from "./zone-of.ts";

const zone = (id: string, value: string | null, perCharacter = true) => ({
  id,
  name: id,
  valueType: "zone",
  default: value,
  bounds: null,
  list: false,
  ranges: false,
  perCharacter,
  memory: false,
  help: "",
});
const blueprint = (id: string, variables: BlueprintSummary_Serialize["variables"]): BlueprintSummary_Serialize => ({
  id,
  name: id,
  description: "",
  given: false,
  version: 1,
  basedOn: null,
  problems: 0,
  variables,
});
const character = (
  name: string,
  runs: string | null,
  settings: Record<string, string> = {},
): PlayedCharacter_Serialize => ({
  key: name,
  name,
  level: 1,
  breedName: null,
  head: null,
  sex: null,
  lastSeen: 0,
  place: null,
  blueprint: runs,
  settings,
  dropped: [],
});

const BLUEPRINTS = [
  blueprint("harvest", [zone("zone", null)]),
  blueprint("astrub", [zone("zone", '{"sub_area":97}')]),
  blueprint("fixed", [zone("zone", '{"sub_area":97}', false)]),
];

describe("whose Zone a sub-area is", () => {
  it("names the characters whose own value or blueprint's default is the sub-area, in their order", () => {
    const played = [
      character("Kalyx", "harvest", { zone: '{"sub_area":97}' }),
      character("Orrin", "astrub"),
      character("Lyra", "astrub", { zone: '{"sub_area":98}' }),
    ];
    expect(zoneOf({ sub_area: 97 }, played, BLUEPRINTS)).toEqual(["Kalyx", "Orrin"]);
    expect(zoneOf({ sub_area: 98 }, played, BLUEPRINTS)).toEqual(["Lyra"]);
  });

  it("names none without a blueprint, with one gone, with a Zone not set per character or not a sub-area", () => {
    const played = [
      character("None", null),
      character("Gone", "deleted", { zone: '{"sub_area":97}' }),
      character("Fixed", "fixed"),
      character("Mine", "harvest", { zone: '{"zone":"wheat"}' }),
      character("Broken", "harvest", { zone: "{" }),
    ];
    expect(zoneOf({ sub_area: 97 }, played, BLUEPRINTS)).toEqual([]);
  });

  it("names the characters whose Zone is a zone of mine, and none for another zone or a sub-area", () => {
    const played = [
      character("Kalyx", "harvest", { zone: '{"zone":"wheat"}' }),
      character("Orrin", "harvest", { zone: '{"zone":"wood"}' }),
      character("Lyra", "astrub"),
    ];
    expect(zoneOf({ zone: "wheat" }, played, BLUEPRINTS)).toEqual(["Kalyx"]);
    expect(zoneOf({ zone: "97" }, played, BLUEPRINTS)).toEqual([]);
  });
});
