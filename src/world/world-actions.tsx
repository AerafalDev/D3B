import { useTranslation } from "react-i18next";

import { named } from "@/characters/names.ts";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { Icon } from "@/icons/icon.tsx";
import { useWorld } from "./world-store.ts";

/** The World tab's editor action: the list of worlds, by name, the one shown chosen (the owner, 2026-11-15). */
export function WorldActions() {
  const { t, i18n } = useTranslation();
  const read = useWorld((store) => store.read);
  const world = useWorld((store) => store.world);
  const show = useWorld((store) => store.show);
  if (read?.state !== "read" || read.atlas.worlds.length === 0) {
    return null;
  }
  const worlds = read.atlas.worlds
    .map((one) => ({ id: one.id, name: named(one.name, i18n.language) ?? String(one.id) }))
    .sort((a, b) => a.name.localeCompare(b.name, i18n.language));
  return (
    <Select value={String(world)} onValueChange={(value) => show(Number(value))}>
      <SelectTrigger size="sm" aria-label={t("world.worlds")} className="h-6 gap-1.5 rounded-[5px] px-2 text-xs">
        <Icon name="globe" className="size-3.5" />
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {worlds.map((one) => (
          <SelectItem key={one.id} value={String(one.id)} className="text-xs">
            {one.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
