import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import { commands, events, type ForgetError, type PlayedCharacter_Serialize, type SettingError } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";
import { useCharacters } from "../character-store.ts";

/** A character played, as the back end lists it (`docs/bot.md`, "In the app"). */
export type PlayedCharacter = PlayedCharacter_Serialize;

interface PlayedStore {
  /** Every character played, the most recently seen first. */
  played: PlayedCharacter[];
  /**
   * Sends a character's file to the system's trash; the list follows through `playedChanged`. A failure is said in a
   * notification at the bottom right (the owner, 2026-11-14), the row staying.
   */
  forget: (character: PlayedCharacter) => Promise<void>;
  /** The character runs `blueprint`, or none; the list follows, a failure said in a notification. */
  setBlueprint: (character: PlayedCharacter, blueprint: string | null) => Promise<void>;
  /** The character's own value of `variable`, as JSON, or `null` to reset it to the blueprint's default. */
  setSetting: (character: PlayedCharacter, variable: string, value: string | null) => Promise<void>;
  /** Reads the list at each change, then once; returns what stops following. */
  follow: () => () => void;
}

/** What a failed Forget says, in the player's language. */
function forgetFailure(error: ForgetError, name: string): string {
  switch (error.kind) {
    case "noSuchCharacter":
      return i18next.t("characters.forgetFailed.gone", { name });
    case "noFolder":
      return i18next.t("characters.forgetFailed.noFolder", { name });
    case "notForgotten":
      return i18next.t("characters.forgetFailed.refused", { name, reason: error.reason });
  }
}

/** What a failed change of a character's blueprint or setting says, in the player's language. */
function settingFailure(error: SettingError, name: string): string {
  if (error.kind === "notSaved") {
    return i18next.t("settings.failed.notSaved", { name, reason: error.reason });
  }
  return i18next.t(`settings.failed.${error.kind}`, { name });
}

/** Runs a change of a character's file, saying a failure in a notification. */
async function changing(name: string, change: () => Promise<null>): Promise<void> {
  try {
    await change();
  } catch (error) {
    // The command rejects with its `SettingError`; anything else is a bug, said as it came.
    const failure = error as SettingError;
    toast.error(typeof failure?.kind === "string" ? settingFailure(failure, name) : String(error));
  }
}

export const usePlayed = create<PlayedStore>((set) => ({
  played: [],

  forget: async (character) => {
    try {
      await commands.forgetCharacter(character.key);
    } catch (error) {
      // The command rejects with its `ForgetError`; anything else is a bug, said as it came.
      const failure = error as ForgetError;
      toast.error(typeof failure?.kind === "string" ? forgetFailure(failure, character.name) : String(error));
    }
  },

  setBlueprint: (character, blueprint) =>
    changing(character.name, () => commands.setCharacterBlueprint(character.key, blueprint)),

  setSetting: (character, variable, value) =>
    changing(character.name, () => commands.setCharacterSetting(character.key, variable, value)),

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    let live = true;
    // Each read is numbered: an older one that ends after a newer one is dropped.
    let asked = 0;
    const refresh = () => {
      const number = ++asked;
      void commands.playedCharacters().then(
        (list) => {
          if (live && number === asked) {
            set({ played: list.characters });
          }
        },
        (error: unknown) => console.error("the characters played could not be read", error),
      );
    };
    // The listener first, then the first read: a change between the two is never lost.
    const unlisten = events.playedChanged.listen(refresh);
    void unlisten.then(refresh);
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  },
}));

/** The characters played whose client is closed: those no connection's view names, the most recently seen first. */
export function useOffline(): PlayedCharacter[] {
  const played = usePlayed((store) => store.played);
  const views = useCharacters((store) => store.views);
  const online = new Set(Object.values(views).map((view) => view.key));
  return played.filter((character) => !online.has(character.key));
}
