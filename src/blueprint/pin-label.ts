import i18next from "i18next";

/**
 * A pin's label in the player's language (`node.pins.<pin>`), or its id when the translations have none: a function's
 * input or output, whose id the player chose.
 */
export function pinLabel(pin: string): string {
  const key = `node.pins.${pin}`;
  // The pins' ids are data, so their keys are checked at run time rather than typed.
  return i18next.exists(key) ? String(i18next.t(key as never)) : pin;
}
