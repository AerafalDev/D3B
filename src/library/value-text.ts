import i18next from "i18next";

/** Whole seconds as the player types them (docs/bot.md, "Types"): "5 min", "1 min 30", "45 s". */
function duration(seconds: number): string {
  const [minutes, rest] = [Math.floor(seconds / 60), seconds % 60];
  if (minutes === 0) {
    return i18next.t("library.value.seconds", { seconds: rest });
  }
  return rest === 0
    ? i18next.t("library.value.minutes", { minutes })
    : i18next.t("library.value.minutesSeconds", { minutes, seconds: rest });
}

/** A value as the player reads it, by its type (`bot::variable`'s shapes); a shape its type does not hold, as JSON. */
export function valueText(valueType: string, json: string | null, language: string): string {
  return shapedText({ valueType, list: false, ranges: false }, json, language);
}

/** A type and shape as the player reads it: "Durée", "Durée · liste", "Zone · par niveau". */
export function typeText(typed: { valueType: string; list?: boolean; ranges?: boolean }): string {
  // A type's name is `ValueType`'s, each with its key under `blueprint.type`.
  const type = i18next.t(`blueprint.type.${typed.valueType}` as never);
  if (typed.ranges === true) {
    return i18next.t("library.value.byLevel", { type });
  }
  return typed.list === true ? i18next.t("library.value.listOf", { type }) : type;
}

/** A value of a type and shape: a list or ranges said by how many values or rows they hold, one value by its type. */
export function shapedText(
  typed: { valueType: string; list?: boolean; ranges?: boolean },
  json: string | null,
  language: string,
): string {
  const held = json === null ? null : parsed(json);
  if (typed.ranges === true && Array.isArray(held)) {
    return i18next.t("library.value.ranges", { count: held.length });
  }
  if (typed.list === true && Array.isArray(held)) {
    return i18next.t("library.value.list", { count: held.length });
  }
  return oneText(typed.valueType, json, language);
}

function parsed(json: string): unknown {
  try {
    return JSON.parse(json);
  } catch {
    return undefined;
  }
}

function oneText(valueType: string, json: string | null, language: string): string {
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
    case "time":
      return typeof value === "string" ? value : json;
    case "map":
      return typeof value === "number" ? i18next.t("library.value.map", { id: value }) : json;
    case "item":
      return typeof value === "number" ? i18next.t("library.value.item", { id: value }) : json;
    case "npc":
      return typeof value === "number" ? i18next.t("library.value.npc", { id: value }) : json;
    case "duration":
      return typeof value === "number" && Number.isInteger(value) && value >= 0 ? duration(value) : json;
    default:
      return json;
  }
}
