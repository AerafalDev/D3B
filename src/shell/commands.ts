import type { Platform } from "@/lib/platform.ts";
import { useLayout } from "./layout-store.ts";

/** The shell's commands, each named under `shell.commands` in the locales. */
export type CommandId = "primarySideBar" | "panel" | "secondarySideBar";

/** A command a shortcut runs; the command palette and the menus list them once they come. */
export interface ShellCommand {
  id: CommandId;
  /** Its shortcut in react-hotkeys-hook's notation; `mod` is Cmd on macOS and Ctrl elsewhere. VS Code's keys. */
  keys: string;
  run: () => void;
}

export const shellCommands: readonly ShellCommand[] = [
  { id: "primarySideBar", keys: "mod+b", run: () => useLayout.getState().toggle("primarySideBar") },
  { id: "panel", keys: "mod+j", run: () => useLayout.getState().toggle("panel") },
  { id: "secondarySideBar", keys: "mod+alt+b", run: () => useLayout.getState().toggle("secondarySideBar") },
];

/** The modifiers' names outside macOS, from the locales (`Maj` in French, as VS Code writes it). */
export interface KeyNames {
  ctrl: string;
  shift: string;
  alt: string;
}

const MAC_KEYS: Record<string, string> = { mod: "⌘", shift: "⇧", alt: "⌥", ctrl: "⌃" };

/** A shortcut as the platform writes it: `⌘⇧P` on macOS, `Ctrl+Shift+P` (or its translation) elsewhere. */
export function keyLabel(keys: string, on: Platform, names: KeyNames): string {
  const named: Record<string, string> =
    on === "mac" ? MAC_KEYS : { mod: names.ctrl, ctrl: names.ctrl, shift: names.shift, alt: names.alt };
  const parts = keys.split("+").map((key) => named[key] ?? key.toUpperCase());
  return parts.join(on === "mac" ? "" : "+");
}
