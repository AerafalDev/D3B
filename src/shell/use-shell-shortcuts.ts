import { useHotkeys } from "react-hotkeys-hook";

import { useAppSettings } from "@/settings/app-settings.ts";
import { offered, shellCommands } from "./commands.ts";

const ALL_KEYS = shellCommands.flatMap((command) => (command.keys === undefined ? [] : [command.keys])).join(",");

/**
 * Runs the shell's commands from their shortcuts, anywhere in the window, fields included, as VS Code does. A command
 * not offered now does nothing, but its key is still taken from the webview, whose own Ctrl+J opens its downloads.
 */
export function useShellShortcuts(): void {
  useHotkeys(
    ALL_KEYS,
    (_event, handler) => {
      const command = shellCommands.find((each) => each.keys === handler.hotkey);
      if (command !== undefined && offered(command, useAppSettings.getState().advancedTools)) {
        command.run();
      }
    },
    { preventDefault: true, enableOnFormTags: true, enableOnContentEditable: true },
  );
}
