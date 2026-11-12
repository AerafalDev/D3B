import { useTranslation } from "react-i18next";

import { platform } from "@/lib/platform.ts";
import { type CommandId, keyLabel, shellCommands } from "./commands.ts";

/** Writes a command's shortcut as this platform and the app's language do. */
export function useShortcut(): (id: CommandId) => string {
  const { t } = useTranslation();
  const names = { ctrl: t("shell.keys.ctrl"), shift: t("shell.keys.shift"), alt: t("shell.keys.alt") };
  const on = platform();
  return (id) => {
    const command = shellCommands.find((each) => each.id === id);
    return command === undefined ? "" : keyLabel(command.keys, on, names);
  };
}
