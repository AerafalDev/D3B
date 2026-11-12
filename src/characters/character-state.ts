import type { GameProtocol } from "@/bindings.ts";
import type { CharacterView } from "./character-store.ts";

/** What a connection's character is doing, as its list row's dot and its tab say it. */
export type CharacterState = "waiting" | "exploring" | "fighting" | "off" | "lost";

/**
 * A connection's state: its protocol first (off when its build is not read, lost when its frames stopped making sense),
 * then whether its character is in the game yet, then in a fight or exploring.
 */
export function characterState(protocol: GameProtocol, view: CharacterView | undefined): CharacterState {
  if (protocol === "lost") {
    return "lost";
  }
  if (protocol !== "on") {
    return "off";
  }
  if (view === undefined || view.name === null) {
    return "waiting";
  }
  return view.fighting === true ? "fighting" : "exploring";
}
