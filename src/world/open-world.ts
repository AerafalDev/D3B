import i18next from "i18next";

import { openEditor } from "@/shell/parts/editor-store.ts";

/** The World tab's editor id: one tab for the whole world. */
export const WORLD_EDITOR = "world";

/** Opens the World tab, or brings it forward. */
export function openWorld(): void {
  openEditor({ id: WORLD_EDITOR, component: "world", title: i18next.t("world.title"), params: { icon: "worldView" } });
}
