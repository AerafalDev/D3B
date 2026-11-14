import i18next from "i18next";

/**
 * A text of one of D3B's blueprints in the player's language (`given.<id>.<path>` in the locales, `bot::given`), or
 * `fallback`, the file's English, when the translations lack it.
 */
export function givenText(id: string, path: string, fallback: string): string {
  const key = `given.${id}.${path}`;
  // The given blueprints' ids are data, so their keys are checked at run time rather than typed.
  return i18next.exists(key) ? String(i18next.t(key as never)) : fallback;
}
