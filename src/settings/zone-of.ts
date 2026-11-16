import type { PlayedCharacter } from "@/characters/played/played-store.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";

/**
 * The characters whose Zone is the sub-area `subArea` (`docs/bot.md`, the World tab's Details "from step 3's PR 11"):
 * those whose blueprint sets a Zone per character that their own value, else the blueprint's default, makes it. Their
 * names, in the order the characters are listed.
 */
export function zoneOf(
  subArea: number,
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
        return json !== null && isSubArea(json, subArea);
      });
    })
    .map((character) => character.name);
}

function isSubArea(json: string, subArea: number): boolean {
  try {
    const value: unknown = JSON.parse(json);
    return typeof value === "object" && value !== null && "sub_area" in value && value.sub_area === subArea;
  } catch {
    return false;
  }
}
