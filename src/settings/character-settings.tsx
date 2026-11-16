import { ArrowUpRight, RotateCcw } from "lucide-react";
import { useTranslation } from "react-i18next";

import type { VariableView_Serialize as VariableView } from "@/bindings.ts";
import { type PlayedCharacter, usePlayed } from "@/characters/played/played-store.ts";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { givenText } from "@/library/given-text.ts";
import { type BlueprintSummary, useLibrary } from "@/library/library-store.ts";
import { blueprintName, openBlueprint } from "@/library/open-blueprint.ts";
import { valueText } from "@/library/value-text.ts";
import { SettingEditor } from "./setting-editor.tsx";

/** The list's value for "no blueprint": no blueprint's id holds a space. */
const NONE = " none";

interface CharacterSettingsProps {
  /** The character's key, `<server>-<id>`, as its file and its connection's view name it. */
  characterKey: string;
}

/**
 * A character's blueprint and its settings, at the top of its tab, live or offline (`docs/bot.md`, "In the app"; the
 * prototype's layout, the owner's choices of 2026-11-16): the blueprint in a list, none at first, an arrow opening it;
 * then each variable it sets per character, the character's own value marked by a bar and reset in one click.
 */
export function CharacterSettings({ characterKey }: CharacterSettingsProps) {
  const { t } = useTranslation();
  const character = usePlayed((store) => store.played.find((one) => one.key === characterKey));
  const blueprints = useLibrary((store) => store.blueprints);
  const setBlueprint = usePlayed((store) => store.setBlueprint);
  if (character === undefined) {
    // Not remembered yet: its file is written as soon as the game says who it is.
    return null;
  }
  const blueprint = blueprints.find((one) => one.id === character.blueprint);

  return (
    <section aria-label={t("settings.title")} className="flex flex-col gap-3">
      <div className="flex flex-col gap-1">
        <span className="text-text-muted text-xs">{t("settings.blueprint")}</span>
        <div className="flex gap-1">
          <Select
            value={character.blueprint ?? NONE}
            onValueChange={(chosen) => void setBlueprint(character, chosen === NONE ? null : chosen)}
          >
            <SelectTrigger
              size="sm"
              aria-label={t("settings.blueprint")}
              className="h-[26px] min-w-0 flex-1 gap-1.5 rounded border-[#3c3c3c] bg-[#181818] px-2 text-xs data-[size=sm]:h-[26px]"
            >
              <SelectValue>
                {character.blueprint !== null && blueprint === undefined
                  ? t("settings.missing", { id: character.blueprint })
                  : undefined}
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={NONE} className="text-xs">
                {t("settings.none")}
              </SelectItem>
              {blueprints.map((one) => (
                <SelectItem key={one.id} value={one.id} className="text-xs">
                  {blueprintName(one)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <button
            type="button"
            disabled={blueprint === undefined}
            aria-label={t("settings.open")}
            title={t("settings.open")}
            onClick={() => blueprint !== undefined && openBlueprint(blueprint.id, blueprintName(blueprint))}
            className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-[26px] shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1 disabled:opacity-40"
          >
            <ArrowUpRight aria-hidden="true" size={16} strokeWidth={1.5} />
          </button>
        </div>
      </div>
      {blueprint?.variables
        .filter((variable) => variable.perCharacter)
        .map((variable) => (
          <SettingRow key={variable.id} character={character} blueprint={blueprint} variable={variable} />
        ))}
    </section>
  );
}

interface SettingRowProps {
  character: PlayedCharacter;
  blueprint: BlueprintSummary;
  variable: VariableView;
}

/** One setting: its name, its help on hover, a bar and a reset when the character holds its own value. */
function SettingRow({ character, blueprint, variable }: SettingRowProps) {
  const { t, i18n } = useTranslation();
  const setSetting = usePlayed((store) => store.setSetting);
  const read = (path: string, fallback: string) =>
    blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
  const name = read(`variables.${variable.id}.name`, variable.name);
  const help = read(`variables.${variable.id}.help`, variable.help);
  const own = character.settings[variable.id];
  const reset = t("settings.reset", { value: valueText(variable.valueType, variable.default, i18n.language) });

  return (
    <div className="relative flex flex-col gap-1">
      {own !== undefined && (
        <span aria-hidden="true" className="bg-focus absolute top-px bottom-px -left-2 w-0.5 rounded-[1px]" />
      )}
      <div className="flex h-[18px] items-center justify-between">
        <span className="text-text-muted text-xs" title={help === "" ? undefined : help}>
          {name}
        </span>
        {own !== undefined && (
          <button
            type="button"
            aria-label={reset}
            title={reset}
            onClick={() => void setSetting(character, variable.id, null)}
            className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 items-center justify-center rounded hover:text-white focus-visible:outline-1"
          >
            <RotateCcw aria-hidden="true" size={14} strokeWidth={1.5} />
          </button>
        )}
      </div>
      <SettingEditor
        variable={variable}
        value={own ?? variable.default}
        onChange={(value) => void setSetting(character, variable.id, value)}
        who={character.name}
        label={name}
      />
    </div>
  );
}
