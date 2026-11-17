/** A duration as the player types it (docs/bot.md, "Types"): minutes, seconds or both, "5 min", "1 min 30", "90 s". */
const TYPED = /^(?:(\d+)\s*min)?\s*(?:(\d+)\s*(s)?)?$/;

/**
 * The whole seconds a duration typed holds, `null` for text that is none: a bare number says no unit, so it is refused
 * rather than taken for minutes or seconds; seconds after minutes are below 60.
 */
export function parseDuration(text: string): number | null {
  const match = TYPED.exec(text.trim().toLowerCase());
  if (match === null) {
    return null;
  }
  const [, minutes, seconds, unit] = match;
  if (minutes === undefined && (seconds === undefined || unit === undefined)) {
    return null;
  }
  if (minutes !== undefined && seconds !== undefined && Number(seconds) >= 60) {
    return null;
  }
  return Number(minutes ?? 0) * 60 + Number(seconds ?? 0);
}
