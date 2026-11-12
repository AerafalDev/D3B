import type { Names, Place } from "@/bindings.ts";

/** A name in the interface's language. */
export function named(names: Names | null | undefined, language: string): string | null {
  if (names == null) {
    return null;
  }
  return language.startsWith("fr") ? names.fr : names.en;
}

/** Where a character is, as its row says it: its coordinates, then its sub-area. */
export function placeName(place: Place | null | undefined, language: string): string | null {
  const at = place?.at;
  if (at == null) {
    return null;
  }
  const subArea = named(place?.subArea, language);
  return subArea === null ? `${at.x}, ${at.y}` : `${at.x}, ${at.y} · ${subArea}`;
}

/** The avatar's letters: the name's first two, the second in lower case. */
export function initials(name: string): string {
  const letters = [...name.replace(/[^\p{L}\p{N}]/gu, "")];
  return `${(letters[0] ?? "?").toUpperCase()}${(letters[1] ?? "").toLowerCase()}`;
}
