import i18next from "i18next";

/**
 * A word of the nodes' namespace (`locales/nodes.<language>.json`): a kind's `kinds.<kind>.sentence` or
 * `.description`, the `.word` and `.between` of a kind of N inputs (its compact node's word, what joins its inputs in
 * its sentence), a pin's `pins.<pin>`, in the player's language, else English; `null` when neither has it. The nodes'
 * words are their own namespace, outside the typed keys, so they grow with the kinds without the types (the owner,
 * 2026-11-17); their keys are data, read at run time.
 */
export function nodeWord(path: string): string | null {
  const found =
    i18next.getResource(i18next.resolvedLanguage ?? i18next.language, "nodes", path) ??
    i18next.getResource("en", "nodes", path);
  return typeof found === "string" ? found : null;
}
