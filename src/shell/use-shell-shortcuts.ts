import { useHotkeys } from "react-hotkeys-hook";

import { shellCommands } from "./commands.ts";

const ALL_KEYS = shellCommands.map((command) => command.keys).join(",");

/** Runs the shell's commands from their shortcuts, anywhere in the window, fields included, as VS Code does. */
export function useShellShortcuts(): void {
  useHotkeys(
    ALL_KEYS,
    (_event, handler) => {
      shellCommands.find((command) => command.keys === handler.hotkey)?.run();
    },
    { preventDefault: true, enableOnFormTags: true, enableOnContentEditable: true },
  );
}
