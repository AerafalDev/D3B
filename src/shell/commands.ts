import type { Platform } from "@/lib/platform.ts";
import { useAppSettings } from "@/settings/app-settings.ts";
import { useLayout } from "./layout-store.ts";

/** The shell's commands, each named under `shell.commands` in the locales. */
export type CommandId = "palette" | "primarySideBar" | "panel" | "secondarySideBar" | "advancedTools";

/** A command the palette lists, the menus offer and a shortcut runs. */
export interface ShellCommand {
  id: CommandId;
  /** Its shortcut in react-hotkeys-hook's notation; `mod` is Cmd on macOS and Ctrl elsewhere. VS Code's keys. */
  keys?: string;
  /** Offered only with Advanced tools on: the panel's only view is the Protocol view until step 4's History. */
  advanced?: true;
  run: () => void;
}

export const shellCommands: readonly ShellCommand[] = [
  { id: "palette", keys: "mod+shift+p", run: () => useLayout.getState().setPaletteOpen(true) },
  { id: "primarySideBar", keys: "mod+b", run: () => useLayout.getState().toggle("primarySideBar") },
  { id: "panel", keys: "mod+j", advanced: true, run: () => useLayout.getState().toggle("panel") },
  { id: "secondarySideBar", keys: "mod+alt+b", run: () => useLayout.getState().toggle("secondarySideBar") },
  {
    id: "advancedTools",
    run: () => {
      const settings = useAppSettings.getState();
      settings.setAdvancedTools(!settings.advancedTools);
    },
  },
];

/** Whether `command` is offered now: in the palette, the menus, and from its shortcut. */
export function offered(command: ShellCommand, advancedTools: boolean): boolean {
  return advancedTools || command.advanced !== true;
}

/** The modifiers' names outside macOS, from the locales (`Maj` in French, as VS Code writes it). */
export interface KeyNames {
  ctrl: string;
  shift: string;
  alt: string;
}

const MAC_KEYS: Record<string, string> = { mod: "⌘", shift: "⇧", alt: "⌥", ctrl: "⌃" };

/**
 * A shortcut as the platform writes it: `⌘⇧P` on macOS, `Ctrl+Shift+P` (or its translation) elsewhere; nothing for a
 * command without one.
 */
export function keyLabel(keys: string | undefined, on: Platform, names: KeyNames): string {
  if (keys === undefined) {
    return "";
  }
  const named: Record<string, string> =
    on === "mac" ? MAC_KEYS : { mod: names.ctrl, ctrl: names.ctrl, shift: names.shift, alt: names.alt };
  const parts = keys.split("+").map((key) => named[key] ?? key.toUpperCase());
  return parts.join(on === "mac" ? "" : "+");
}
