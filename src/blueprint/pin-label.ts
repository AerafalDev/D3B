import i18next from "i18next";

import type { NodeView_Serialize } from "@/bindings.ts";
import { valueText } from "@/library/value-text.ts";
import { nameOf } from "@/values/value-words.ts";

/**
 * A pin's label in the player's language (`node.pins.<pin>`), or its id when the translations have none: a function's
 * input or output, whose id the player chose. Of `node`, a numbered output is said with its number, "Alors 2", and a
 * Switch's case by its value, a profession or a zone by its name (`docs/bot.md`, "The flow's kinds (v2.3)"), and a
 * call's named exit by its name.
 */
export function pinLabel(pin: string, node?: NodeView_Serialize): string {
  // A call's named exit by the name the player gave it, whatever its id.
  const exit = node?.exits?.find((one) => one.id === pin);
  if (exit !== undefined) {
    return exit.name;
  }
  const numbered = /^(then|case)_([1-9][0-9]*)$/.exec(pin);
  if (node !== undefined && numbered !== null) {
    const [, prefix, number] = numbered;
    if (prefix === "then" && node.adds?.pin === "then") {
      return i18next.t("blueprint.pin.then", { number });
    }
    const json = node.options?.cases?.[Number(number) - 1];
    if (prefix === "case" && node.takesCases === true && json !== undefined) {
      const valueType = node.options?.valueType ?? "text";
      return caseText(valueType, json);
    }
  }
  const key = `node.pins.${pin}`;
  // The pins' ids are data, so their keys are checked at run time rather than typed.
  return i18next.exists(key) ? String(i18next.t(key as never)) : pin;
}

/** A case by its value: a profession or a zone by its name, as Details shows it, anything else as the player reads it. */
function caseText(valueType: string, json: string): string {
  try {
    const named = nameOf(valueType, JSON.parse(json), i18next.language);
    if (named !== null) {
      return named;
    }
  } catch {
    // Not JSON: said as it is, below.
  }
  return valueText(valueType, json, i18next.language);
}
