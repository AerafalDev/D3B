import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import {
  type BlueprintSummary_Serialize,
  type CopyTexts,
  commands,
  type DuplicateError,
  events,
  type KeptFile,
} from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";
import { givenText } from "./given-text.ts";

/** A blueprint as the Library lists it (`docs/bot.md`, "In the app"). */
export type BlueprintSummary = BlueprintSummary_Serialize;

interface LibraryStore {
  /** D3B's blueprints, then mine. */
  blueprints: BlueprintSummary[];
  /** The player's files kept as they are, blueprints' and characters' (the owner, 2026-11-14: one "Ignored" group). */
  kept: KeptFile[];
  /** Reads the list again; a failure is said in a notification. */
  refresh: () => Promise<void>;
  /**
   * Duplicate: a copy among mine, named "<name> (copie)" and worded in the player's language; the copy's id, or `null`
   * when it was not made, said in a notification.
   */
  duplicate: (source: BlueprintSummary) => Promise<string | null>;
  /** Reads the list now and when a character's file changes; returns what stops following. */
  follow: () => () => void;
}

/** A copy's texts: the source's as the player reads them, its name marked as a copy. */
function copyTexts(source: BlueprintSummary): CopyTexts {
  const read = (path: string, fallback: string) => (source.given ? givenText(source.id, path, fallback) : fallback);
  const variables = Object.fromEntries(
    source.variables.map((variable) => [
      variable.id,
      {
        name: read(`variables.${variable.id}.name`, variable.name),
        help: read(`variables.${variable.id}.help`, variable.help),
      },
    ]),
  );
  return {
    name: i18next.t("library.copyName", { name: read("name", source.name) }),
    description: read("description", source.description),
    variables,
  };
}

/** What a failed Duplicate says, in the player's language. */
function duplicateFailure(error: DuplicateError, name: string): string {
  switch (error.kind) {
    case "noSuchBlueprint":
      return i18next.t("library.duplicateFailed.gone", { name });
    case "noFolder":
      return i18next.t("library.duplicateFailed.noFolder", { name });
    case "notSaved":
      return i18next.t("library.duplicateFailed.notSaved", { name, reason: error.reason });
  }
}

export const useLibrary = create<LibraryStore>((set, get) => ({
  blueprints: [],
  kept: [],

  refresh: async () => {
    try {
      const [library, played] = await Promise.all([commands.blueprints(), commands.playedCharacters()]);
      set({ blueprints: library.blueprints, kept: [...library.kept, ...played.kept] });
    } catch (error) {
      toast.error(i18next.t("library.unread", { reason: String(error) }));
    }
  },

  duplicate: async (source) => {
    try {
      const id = await commands.duplicateBlueprint(source.id, copyTexts(source));
      await get().refresh();
      return id;
    } catch (error) {
      const failure = error as DuplicateError;
      const name = source.given ? givenText(source.id, "name", source.name) : source.name;
      toast.error(typeof failure?.kind === "string" ? duplicateFailure(failure, name) : String(error));
      return null;
    }
  },

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    const refresh = () => void get().refresh();
    const unlisten = events.playedChanged.listen(refresh);
    void unlisten.then(refresh);
    return () => {
      void unlisten.then((stop) => stop());
    };
  },
}));
