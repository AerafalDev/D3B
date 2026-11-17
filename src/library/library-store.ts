import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import {
  type BlueprintSummary_Serialize,
  type CopyTexts,
  commands,
  type DeleteError,
  type DuplicateError,
  events,
  type KeptFile,
} from "@/bindings.ts";
import { useGraphs } from "@/blueprint/graph-store.ts";
import { isTauri } from "@/lib/tauri.ts";
import { givenText } from "./given-text.ts";

/** A blueprint as the Library lists it (`docs/bot.md`, "In the app"). */
export type BlueprintSummary = BlueprintSummary_Serialize;

interface LibraryStore {
  /** D3B's blueprints, then mine. */
  blueprints: BlueprintSummary[];
  /** The player's files kept as they are, blueprints', characters' and zones' (the owner, 2026-11-14: one "Ignored"
   * group). */
  kept: KeptFile[];
  /** Reads the list again; a failure is said in a notification. */
  refresh: () => Promise<void>;
  /**
   * Duplicate: a copy among mine, named "<name> (copie)" and worded in the player's language; the copy's id, or `null`
   * when it was not made, said in a notification.
   */
  duplicate: (source: BlueprintSummary) => Promise<string | null>;
  /**
   * Delete: one of mine to the system's trash, without asking first, as Forget (the owner, 2026-11-16); whether it
   * went, a failure said in a notification.
   */
  remove: (blueprint: BlueprintSummary) => Promise<boolean>;
  /**
   * Renames one of mine, its name and description, its id kept (the owner, 2026-11-17): saved at once and undone as
   * the editor's changes; whether it was, a refusal said in a notification.
   */
  rename: (blueprint: BlueprintSummary, name: string, description: string) => Promise<boolean>;
  /** Reads the list now and when a character's file changes; returns what stops following. */
  follow: () => () => void;
}

/** A blueprint's texts as the player reads them: a given one's in their language, since its file's are English. */
export function playerTexts(source: BlueprintSummary): CopyTexts {
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
  return { name: read("name", source.name), description: read("description", source.description), variables };
}

/** A copy's texts: the source's as the player reads them, its name marked as a copy. */
function copyTexts(source: BlueprintSummary): CopyTexts {
  const texts = playerTexts(source);
  return { ...texts, name: i18next.t("library.copyName", { name: texts.name }) };
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

/** What a failed Delete says, in the player's language. */
function deleteFailure(error: DeleteError, name: string): string {
  switch (error.kind) {
    case "noSuchBlueprint":
      return i18next.t("library.deleteFailed.gone", { name });
    case "noFolder":
      return i18next.t("library.deleteFailed.noFolder", { name });
    case "locked":
      return i18next.t("library.deleteFailed.locked", { name });
    case "notDeleted":
      return i18next.t("library.deleteFailed.notDeleted", { name, reason: error.reason });
  }
}

// Each read is numbered: an older one that ends after a newer one is dropped.
let asked = 0;

export const useLibrary = create<LibraryStore>((set, get) => ({
  blueprints: [],
  kept: [],

  refresh: async () => {
    const number = ++asked;
    try {
      const [library, played, zones] = await Promise.all([
        commands.blueprints(),
        commands.playedCharacters(),
        // My zones' files kept join the others; a failure to list them leaves the Library whole.
        commands.zones().catch(() => ({ kept: [] })),
      ]);
      if (number === asked) {
        set({ blueprints: library.blueprints, kept: [...library.kept, ...played.kept, ...zones.kept] });
      }
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

  rename: async (blueprint, name, description) => {
    // The blueprint's, whichever graph shows: through the editor's queue, so it lands in its order and Ctrl+Z undoes it.
    const saved = await useGraphs.getState().edit(blueprint.id, "", { edit: "about", name, description });
    if (saved) {
      await get().refresh();
    }
    return saved;
  },

  remove: async (blueprint) => {
    try {
      await commands.deleteBlueprint(blueprint.id);
      await get().refresh();
      return true;
    } catch (error) {
      const failure = error as DeleteError;
      toast.error(typeof failure?.kind === "string" ? deleteFailure(failure, blueprint.name) : String(error));
      return false;
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
