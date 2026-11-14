import i18next from "i18next";

/**
 * A variable's value as the player reads it, from its type and its JSON (`bot::variable`'s shapes): "90 %", "La plus
 * proche", "Rien", "à choisir" when there is none. A value of a shape the type does not hold reads as its JSON.
 */
export function valueText(valueType: string, json: string | null, language: string): string {
  if (json === null) {
    return i18next.t("library.value.none");
  }
  let value: unknown;
  try {
    value = JSON.parse(json);
  } catch {
    return json;
  }
  const number = new Intl.NumberFormat(language);
  switch (valueType) {
    case "percentage":
      return typeof value === "number" ? i18next.t("library.value.percent", { value: number.format(value) }) : json;
    case "number":
      return typeof value === "number" ? number.format(value) : json;
    case "bool":
      return typeof value === "boolean" ? i18next.t(value ? "library.value.yes" : "library.value.no") : json;
    case "text":
      return typeof value === "string" ? value : json;
    case "bank": {
      if (value === "nearest") {
        return i18next.t("library.value.nearestBank");
      }
      const map = (value as { map?: unknown } | null)?.map;
      return typeof map === "number" ? i18next.t("library.value.bankOn", { map }) : json;
    }
    case "items":
      if (value === "all" || value === "none") {
        return i18next.t(value === "all" ? "library.value.allItems" : "library.value.noItem");
      }
      return Array.isArray(value) ? i18next.t("library.value.items", { count: value.length }) : json;
    case "zone":
      return i18next.t("library.value.zone");
    default:
      return json;
  }
}
