import {
  ArrowLeftRight,
  CircleAlert,
  CircleArrowDown,
  Copy,
  FileWarning,
  Library,
  LibraryBig,
  type LucideIcon,
  PanelBottom,
  PanelLeft,
  PanelRight,
  Pause,
  Search,
  TriangleAlert,
  User,
  Workflow,
  X,
} from "lucide-react";

/** An icon of the app and, for the few that mark a problem or an offer, its one colour. */
interface AppIcon {
  icon: LucideIcon;
  tone?: string;
}

/**
 * The Lucide icons the app uses, by name (the owner, 2026-11-13: Lucide for every icon). Grey as the text around them
 * but for the colours the owner chose from mock-ups: a warning yellow, an error red, an update orange. The window's buttons
 * draw Windows' own glyphs instead (`src/shell/title-bar/window-glyph.tsx`).
 */
export const icons = {
  arrowLeftRight: { icon: ArrowLeftRight },
  blueprint: { icon: Workflow },
  close: { icon: X },
  copy: { icon: Copy },
  error: { icon: CircleAlert, tone: "text-[#db5c5c]" },
  fileWarning: { icon: FileWarning },
  library: { icon: Library },
  libraryView: { icon: LibraryBig },
  panelBottom: { icon: PanelBottom },
  panelLeft: { icon: PanelLeft },
  panelRight: { icon: PanelRight },
  pause: { icon: Pause },
  search: { icon: Search },
  update: { icon: CircleArrowDown, tone: "text-[#c77d55]" },
  user: { icon: User },
  warning: { icon: TriangleAlert, tone: "text-[#f2c55c]" },
} as const satisfies Record<string, AppIcon>;

export type IconName = keyof typeof icons;
