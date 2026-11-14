import i18next from "i18next";
import { useEffect, useState } from "react";

/** Now, in milliseconds, read again every `every` milliseconds, so "Seen 2 hr. ago" keeps up with the clock. */
export function useNow(every = 60_000): number {
  const [now, setNow] = useState(Date.now);
  useEffect(() => {
    const ticking = setInterval(() => setNow(Date.now()), every);
    return () => clearInterval(ticking);
  }, [every]);
  return now;
}

/**
 * How long ago `then` was, at `now` (both in milliseconds), in the player's language, as the owner's mock-up 8 says
 * it: "il y a 5 min", "il y a 2 h", "hier", "il y a 12 jours"; `null` under a minute, and for a time ahead of `now`
 * (a clock set back), which the list says in its own words. Each unit is chosen once rounded, so 59 min 40 s is
 * "il y a 1 h".
 */
export function seenAgo(then: number, now: number, language: string): string | null {
  const minutes = Math.round((now - then) / 60_000);
  if (minutes < 1) {
    return null;
  }
  const short = new Intl.RelativeTimeFormat(language, { numeric: "auto", style: "short" });
  if (minutes < 60) {
    return short.format(-minutes, "minute");
  }
  const hours = Math.round(minutes / 60);
  if (hours < 24) {
    return short.format(-hours, "hour");
  }
  return new Intl.RelativeTimeFormat(language, { numeric: "auto" }).format(-Math.round(hours / 24), "day");
}

/** When `then` was, at `now`, for a sentence: "aujourd'hui à 11:40", "hier à 11:40", "le 2 nov. 2026 à 11:40". */
export function seenOn(then: number, now: number, language: string): string {
  const time = new Intl.DateTimeFormat(language, { timeStyle: "short" }).format(then);
  const day = (moment: number) => new Date(moment).toDateString();
  if (day(then) === day(now)) {
    return i18next.t("character.today", { time });
  }
  if (day(then) === day(now - 86_400_000)) {
    return i18next.t("character.yesterday", { time });
  }
  const date = new Intl.DateTimeFormat(language, { dateStyle: "medium" }).format(then);
  return i18next.t("character.onDate", { date, time });
}
