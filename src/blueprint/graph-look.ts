import {
  ArrowDownToLine,
  Bell,
  Equal,
  Footprints,
  GitBranch,
  Heart,
  HeartPulse,
  Hourglass,
  Landmark,
  LogIn,
  LogOut,
  type LucideIcon,
  Package,
  Play,
  Plus,
  Skull,
  Sprout,
  Square,
  SquareFunction,
  Swords,
  TriangleAlert,
  Variable,
} from "lucide-react";

/** The colour of a family's icon, the prototype's approved look. */
export const FAMILY_COLOUR: Record<string, string> = {
  event: "#f48771",
  action: "#4daafc",
  flow: "#a8a8a8",
  query: "#3fb950",
  function: "#c586c0",
  call: "#c586c0",
  variable: "#a8a8a8",
};

/** Each kind's icon, the prototype's where it had one. */
const KIND_ICON: Record<string, LucideIcon> = {
  on_start: Play,
  life_below: Heart,
  pods_above: Package,
  fight_starts: Swords,
  died: Skull,
  action_failed: TriangleAlert,
  go_to_zone: Footprints,
  harvest_until: Sprout,
  hunt_until: Swords,
  go_to_bank: Landmark,
  deposit_all_but: ArrowDownToLine,
  notify: Bell,
  if: GitBranch,
  wait: Hourglass,
  wait_for_life: HeartPulse,
  finish: Square,
  life_percent: Heart,
  pods_percent: Package,
  level: Equal,
  at_least: Equal,
  add: Plus,
  job_resources: Sprout,
  entry: LogIn,
  return_done: LogOut,
  return_failed: LogOut,
};

/** A node's icon: its kind's, a call's function, a variable's, or a warning for what this version does not know. */
export function nodeIcon(family: string | null, kind: string | null): LucideIcon {
  if (family === null) {
    return TriangleAlert;
  }
  if (family === "call") {
    return SquareFunction;
  }
  if (family === "variable") {
    return Variable;
  }
  return (kind !== null && KIND_ICON[kind]) || Play;
}
