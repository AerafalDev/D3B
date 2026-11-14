import { openEditor } from "@/shell/parts/editor-store.ts";
import { givenText } from "./given-text.ts";
import type { BlueprintSummary } from "./library-store.ts";

/** The editor id of a blueprint's tab. */
export const BLUEPRINT_EDITOR = "blueprint:";

/** Opens a blueprint in its tab, or brings that tab forward. */
export function openBlueprint(id: string, title: string): void {
  openEditor({ id: `${BLUEPRINT_EDITOR}${id}`, component: "blueprint", title, params: { icon: "blueprint", id } });
}

/** A blueprint's name as the player reads it: a given one's in their language. */
export function blueprintName(blueprint: BlueprintSummary): string {
  return blueprint.given ? givenText(blueprint.id, "name", blueprint.name) : blueprint.name;
}
