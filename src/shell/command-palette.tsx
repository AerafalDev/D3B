import { useTranslation } from "react-i18next";
import { useOffline, usePlayed } from "@/characters/played/played-store.ts";
import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandInput,
  CommandItem,
  CommandList,
  CommandShortcut,
} from "@/components/ui/command.tsx";
import { shellCommands } from "./commands.ts";
import { useLayout } from "./layout-store.ts";
import { useShortcut } from "./use-shortcut.ts";

/**
 * VS Code's command palette: every command with its shortcut, filtered as the player types, and Forget for each
 * character played whose client is closed, so it is reached by keyboard on every system (the owner, 2026-11-14).
 */
export function CommandPalette() {
  const { t } = useTranslation();
  const open = useLayout((store) => store.paletteOpen);
  const setOpen = useLayout((store) => store.setPaletteOpen);
  const shortcut = useShortcut();
  const offline = useOffline();
  const forget = usePlayed((store) => store.forget);

  return (
    <CommandDialog
      open={open}
      onOpenChange={setOpen}
      title={t("shell.commands.palette")}
      description={t("shell.palette.placeholder")}
      className="d3b-palette top-[39px] max-w-[600px] rounded-lg! border border-[#454545] bg-[#222222] shadow-2xl sm:max-w-[600px]"
    >
      <Command className="rounded-lg! bg-transparent">
        <CommandInput placeholder={t("shell.palette.placeholder")} />
        <CommandList>
          <CommandEmpty>{t("shell.palette.empty")}</CommandEmpty>
          {shellCommands.map((command) => (
            <CommandItem
              key={command.id}
              value={t(`shell.commands.${command.id}`)}
              onSelect={() => {
                setOpen(false);
                command.run();
              }}
              className="h-[26px] rounded-[4px] text-[13px] data-selected:bg-[#04395e]"
            >
              {t(`shell.commands.${command.id}`)}
              <CommandShortcut className="text-text-muted tracking-normal">{shortcut(command.id)}</CommandShortcut>
            </CommandItem>
          ))}
          {offline.map((character) => (
            <CommandItem
              key={character.key}
              value={t("characters.forget", { name: character.name })}
              onSelect={() => {
                setOpen(false);
                void forget(character);
              }}
            >
              {t("characters.forget", { name: character.name })}
            </CommandItem>
          ))}
        </CommandList>
      </Command>
    </CommandDialog>
  );
}
