import { create } from "zustand";

import { commands, events, type PlayedCharacter_Serialize } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";

/** A character played, as the back end lists it (`docs/bot.md`, "In the app"). */
export type PlayedCharacter = PlayedCharacter_Serialize;

interface PlayedStore {
  /** Every character played, the most recently seen first. */
  played: PlayedCharacter[];
  /** Reads the list again. */
  refresh: () => Promise<void>;
  /** Sends a character's file to the system's trash; the list follows through `playedChanged`. */
  forget: (key: string) => Promise<void>;
  /** Reads the list, then again at each change; returns what stops following. */
  follow: () => () => void;
}

export const usePlayed = create<PlayedStore>((set) => ({
  played: [],

  refresh: async () => {
    set({ played: await commands.playedCharacters() });
  },

  forget: async (key) => {
    await commands.forgetCharacter(key);
  },

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    let live = true;
    const refresh = () => {
      void commands.playedCharacters().then((played) => {
        if (live) {
          set({ played });
        }
      });
    };
    refresh();
    const unlisten = events.playedChanged.listen(refresh);
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  },
}));

/**
 * How long ago `then` was, at `now` (both in milliseconds), in the player's language, short: "il y a 2 h"; `null`
 * under a minute, which the list says in its own words.
 */
export function seenAgo(then: number, now: number, language: string): string | null {
  const format = new Intl.RelativeTimeFormat(language, { numeric: "auto", style: "short" });
  const seconds = Math.round((then - now) / 1000);
  const steps: [Intl.RelativeTimeFormatUnit, number][] = [
    ["day", 86_400],
    ["hour", 3600],
    ["minute", 60],
  ];
  for (const [unit, size] of steps) {
    if (Math.abs(seconds) >= size) {
      return format.format(Math.round(seconds / size), unit);
    }
  }
  return null;
}
