import {
  Ampersand,
  ArrowDownToLine,
  ArrowLeftRight,
  Ban,
  Bell,
  ChevronLeft,
  ChevronRight,
  ChevronsDown,
  ChevronsUp,
  CirclePause,
  CircleSlash,
  Clock,
  Coins,
  Dices,
  Divide,
  Equal,
  EqualNot,
  Footprints,
  GitBranch,
  Hash,
  Heart,
  HeartPulse,
  Hourglass,
  Landmark,
  List,
  ListChecks,
  ListOrdered,
  LogIn,
  LogOut,
  type LucideIcon,
  Merge,
  Minus,
  MoveHorizontal,
  Package,
  Percent,
  Play,
  Plus,
  RefreshCw,
  Repeat,
  Repeat1,
  RotateCw,
  Shrink,
  Shuffle,
  Signpost,
  Skull,
  Split,
  Sprout,
  Square,
  SquareFunction,
  Swords,
  Timer,
  TriangleAlert,
  Undo2,
  Variable,
  X,
} from "lucide-react";

/** The colour of a family's icon, the prototype's approved look. */
export const FAMILY_COLOUR: Record<string, string> = {
  event: "#f48771",
  action: "#4daafc",
  flow: "#a8a8a8",
  query: "#3fb950",
  function: "#c586c0",
  call: "#c586c0",
  // A value is read as a query is: their green (settled alone).
  value: "#3fb950",
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
  sequence: ListOrdered,
  switch: Split,
  repeat: Repeat,
  while: RefreshCw,
  for_each: List,
  leave_loop: Undo2,
  do_n_times: Repeat1,
  alternate: ArrowLeftRight,
  pick: ListChecks,
  wait_until: Timer,
  block: Ban,
  pause_notify: CirclePause,
  retry: RotateCw,
  equal: Equal,
  not_equal: EqualNot,
  less: ChevronLeft,
  less_or_equal: ChevronLeft,
  greater: ChevronRight,
  between: MoveHorizontal,
  and: Ampersand,
  or: Merge,
  not: CircleSlash,
  choose: Signpost,
  subtract: Minus,
  multiply: X,
  divide: Divide,
  remainder: Hash,
  min: ChevronsDown,
  max: ChevronsUp,
  clamp: Shrink,
  percent_of: Percent,
  seconds: Clock,
  random_number: Dices,
  chance: Coins,
  random_element: Shuffle,
  life_percent: Heart,
  pods_percent: Package,
  level: Equal,
  greater_or_equal: Equal,
  minutes: Hourglass,
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

// How far a wire going back leaves its output and comes into its input, how far under the nodes it runs, and the
// radius of its corners.
const OUT = 24;
const UNDER = 40;
const CORNER = 10;

/**
 * A wire's path, from its output to its input; `bottom` is the lowest edge of the two nodes it joins. Forward, a
 * curve; going back (a loop, its input left of its output), out to the right, under both nodes and into its input from
 * the left, so the loop shows whole instead of a curve that leaves the screen (the owner, 2026-11-16).
 */
export function wirePath(from: { x: number; y: number }, to: { x: number; y: number }, bottom: number): string {
  // Forward only with room for a curve; nearer, the loop reads better than a curve folded on itself.
  if (to.x >= from.x + 2 * OUT) {
    const pull = Math.max(40, (to.x - from.x) / 2);
    return `M${from.x},${from.y} C${from.x + pull},${from.y} ${to.x - pull},${to.y} ${to.x},${to.y}`;
  }
  // An input just right of its output (under 2 * OUT) still comes in from the left of the way out, never back over it.
  const out = from.x + OUT;
  const back = Math.min(to.x - OUT, out - 2 * CORNER);
  const low = Math.max(bottom, from.y, to.y) + UNDER;
  return [
    `M${from.x},${from.y}`,
    `L${out - CORNER},${from.y} Q${out},${from.y} ${out},${from.y + CORNER}`,
    `L${out},${low - CORNER} Q${out},${low} ${out - CORNER},${low}`,
    `L${back + CORNER},${low} Q${back},${low} ${back},${low - CORNER}`,
    `L${back},${to.y + CORNER} Q${back},${to.y} ${back + CORNER},${to.y}`,
    `L${to.x},${to.y}`,
  ].join(" ");
}
