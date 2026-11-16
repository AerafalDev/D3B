import i18next from "i18next";
import { toast } from "sonner";
import { create } from "zustand";

import { commands, type SharedPreview_Serialize, type ShareFailure } from "@/bindings.ts";
import { type BlueprintSummary, playerTexts, useLibrary } from "@/library/library-store.ts";
import { blueprintName, openBlueprint } from "@/library/open-blueprint.ts";

/** A `.d3b` file read, as the import's preview shows it (`docs/bot.md`, "Storage"). */
export type SharedPreview = SharedPreview_Serialize;

interface ShareStore {
  /** The `.d3b` shown in the import's preview; none when it is closed. */
  preview: SharedPreview | null;
  /** While the blueprint previewed is saved: the dialog's button waits. */
  importing: boolean;
  /** Export: the blueprint written where the player chooses in the system's save dialog, then said in a notification. */
  exportBlueprint: (source: BlueprintSummary) => Promise<void>;
  /** Import's first step from the system's open dialog: the file chosen, previewed. */
  pick: () => Promise<void>;
  /** The `.d3b` at `path`, dropped on the window, previewed; ignored while an import is saved. */
  open: (path: string) => Promise<void>;
  /** Imports the blueprint previewed among mine and opens its tab; the preview closes. */
  confirm: () => Promise<void>;
  /** Closes the preview without importing. */
  cancel: () => void;
}

/** Why a share failed, in the player's language. */
function shareFailure(error: ShareFailure): string {
  switch (error.kind) {
    case "unreadable":
      return i18next.t("share.failure.unreadable", { reason: error.reason });
    case "newer":
      return i18next.t("share.failure.newer", { found: error.found });
    case "newerSchema":
      return i18next.t("share.failure.newerSchema", { found: error.found });
    case "newerZone":
      return i18next.t("share.failure.newerZone", { found: error.found });
    case "notOfItsShape":
      return i18next.t("share.failure.notOfItsShape", { detail: error.detail });
    case "notSaved":
      return i18next.t("share.failure.notSaved", { reason: error.reason });
    default:
      return i18next.t(`share.failure.${error.kind}`);
  }
}

/** What the back end refused, worded; a bug's own text when it is no `ShareFailure`. */
function reason(error: unknown): string {
  const failure = error as ShareFailure | undefined;
  return typeof failure?.kind === "string" ? shareFailure(failure) : String(error);
}

/** A path's last part: the file's name. */
function fileName(path: string): string {
  return path.split(/[\\/]/).pop() ?? path;
}

export const useShare = create<ShareStore>((set, get) => ({
  preview: null,
  importing: false,

  exportBlueprint: async (source) => {
    const name = blueprintName(source);
    try {
      // One of D3B's goes worded as the player reads it, since its file's texts are English.
      const path = await commands.exportBlueprint(source.id, source.given ? playerTexts(source) : null);
      if (path !== null) {
        toast.success(i18next.t("share.exported", { name, file: fileName(path) }));
      }
    } catch (error) {
      toast.error(i18next.t("share.exportFailed", { name, reason: reason(error) }));
    }
  },

  pick: async () => {
    try {
      const preview = await commands.pickShared();
      if (preview !== null) {
        set({ preview });
      }
    } catch (error) {
      toast.error(i18next.t("share.importFailed", { reason: reason(error) }));
    }
  },

  open: async (path) => {
    // A file dropped while one is imported would be closed by the import's end without a word.
    if (get().importing) {
      return;
    }
    try {
      set({ preview: await commands.previewShared(path) });
    } catch (error) {
      toast.error(i18next.t("share.importFailed", { reason: reason(error) }));
    }
  },

  confirm: async () => {
    const { preview, importing } = get();
    if (preview === null || importing) {
      return;
    }
    set({ importing: true });
    try {
      const id = await commands.importShared(preview.path);
      set({ preview: null });
      await useLibrary.getState().refresh();
      openBlueprint(id, preview.blueprint.name);
    } catch (error) {
      toast.error(i18next.t("share.importFailed", { reason: reason(error) }));
    } finally {
      set({ importing: false });
    }
  },

  cancel: () => set({ preview: null }),
}));
