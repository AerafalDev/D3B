import type { ComponentType } from "react";

import { CharactersView } from "@/characters/characters-view.tsx";
import type { IconName } from "@/icons/icons.ts";
import { LibraryView } from "@/library/library-view.tsx";
import type { ViewId } from "./layout-store.ts";

/** A view of the primary side bar: its activity bar entry and what it shows. */
export interface SideView {
  id: ViewId;
  /** A 20 px icon, as IntelliJ's side bars use. */
  icon: IconName;
  /** Its name, in the activity bar's hover and the side bar's header. */
  title: "characters.title" | "library.title";
  component: ComponentType;
}

/** The side bar's views, in the activity bar's order; each feature with a view adds its own. */
export const SIDE_VIEWS: readonly SideView[] = [
  { id: "characters", icon: "user", title: "characters.title", component: CharactersView },
  { id: "library", icon: "libraryView", title: "library.title", component: LibraryView },
];
