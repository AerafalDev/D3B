import type { PlayedCharacter } from "@/characters/played/played-store.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import type { ZoneValue } from "@/zone/zone-field.tsx";

/**
 * The characters whose Zone is `zone`, a sub-area or a zone of mine (`docs/bot.md`, the World tab's Details "from step 3's PR 11"):
 * those whose blueprint sets a Zone per character that their own value, else the blueprint's default, makes it. Their
 * names, in the order the characters are listed.
 */
export function zoneOf(
  zone: ZoneValue,
  played: readonly PlayedCharacter[],
  blueprints: readonly BlueprintSummary[],
): string[] {
  return played
    .filter((character) => {
      const blueprint = blueprints.find((one) => one.id === character.blueprint);
      return (blueprint?.variables ?? []).some((variable) => {
        if (variable.valueType !== "zone" || !variable.perCharacter) {
          return false;
        }
        const json = character.settings[variable.id] ?? variable.default;
        return json !== null && isZone(json, zone);
      });
    })
    .map((character) => character.name);
}

function isZone(json: string, zone: ZoneValue): boolean {
  try {
    const value: unknown = JSON.parse(json);
    if (typeof value !== "object" || value === null) {
      return false;
    }
    return "zone" in zone
      ? "zone" in value && value.zone === zone.zone
      : "sub_area" in value && value.sub_area === zone.sub_area;
  } catch {
    return false;
  }
}
