import {
  ArrowLeftRight,
  CircleAlert,
  CircleArrowDown,
  Copy,
  Library,
  type LucideIcon,
  Minus,
  PanelBottom,
  PanelLeft,
  PanelRight,
  Pause,
  Search,
  Square,
  TriangleAlert,
  User,
  X,
} from "lucide-react";

/** An icon of the app and, for the few that mark a problem or an offer, its one colour. */
interface AppIcon {
  icon: LucideIcon;
  tone?: string;
}

/**
 * The Lucide icons the app uses, by name (the owner, 2026-11-13: Lucide for every icon). Grey as the text around them
 * but for the colours the owner chose from mock-ups: a warning yellow, an error red, an update orange.
 */
export const icons = {
  arrowLeftRight: { icon: ArrowLeftRight },
  close: { icon: X },
  error: { icon: CircleAlert, tone: "text-[#db5c5c]" },
  library: { icon: Library },
  panelBottom: { icon: PanelBottom },
  panelLeft: { icon: PanelLeft },
  panelRight: { icon: PanelRight },
  pause: { icon: Pause },
  search: { icon: Search },
  update: { icon: CircleArrowDown, tone: "text-[#c77d55]" },
  user: { icon: User },
  warning: { icon: TriangleAlert, tone: "text-[#f2c55c]" },
  windowClose: { icon: X },
  windowMaximize: { icon: Square },
  windowMinimize: { icon: Minus },
  windowRestore: { icon: Copy },
} as const satisfies Record<string, AppIcon>;

export type IconName = keyof typeof icons;
