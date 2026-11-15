import type { Atlas_Serialize } from "@/bindings.ts";
import { named } from "@/characters/names.ts";

/** A sub-area as the World view lists it: drawn when a world shown holds its maps. */
export interface TreeSubArea {
  id: number;
  name: string;
  level: number;
  drawn: boolean;
}

/** An area and the sub-areas the view lists under it. */
export interface TreeArea {
  id: number;
  name: string;
  subAreas: TreeSubArea[];
}

/** Lowercase and without accents, so "foret" finds "Forêt". */
function folded(text: string): string {
  return text
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

/**
 * The World view's tree (the owner, 2026-11-15): the areas by name in the player's language, their sub-areas by level,
 * then by name;
 * a search keeps an area whose name holds it whole, else its sub-areas whose names hold it, and drops the areas left
 * empty.
 */
export function worldTree(atlas: Atlas_Serialize, language: string, query: string): TreeArea[] {
  const sought = folded(query.trim());
  const order = (a: { name: string }, b: { name: string }) => a.name.localeCompare(b.name, language);
  return atlas.areas
    .map((area) => {
      const name = named(area.name, language) ?? "";
      const subAreas = area.subAreas
        .map((subArea) => ({
          id: subArea.id,
          name: named(subArea.name, language) ?? "",
          level: subArea.level,
          drawn: subArea.maps.length > 0,
        }))
        .filter((subArea) => sought === "" || folded(name).includes(sought) || folded(subArea.name).includes(sought))
        .sort((a, b) => a.level - b.level || order(a, b));
      return { id: area.id, name, subAreas };
    })
    .filter((area) => area.subAreas.length > 0)
    .sort(order);
}
