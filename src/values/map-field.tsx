import { Crosshair, MapPin } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { commands, type MapView_Serialize } from "@/bindings.ts";
import { useCharacters } from "@/characters/character-store.ts";
import { named } from "@/characters/names.ts";
import { openWorld } from "@/world/open-world.ts";
import { useWorld } from "@/world/world-store.ts";

interface MapFieldProps {
  /** The map chosen, its id as JSON; `null` while none is. */
  value: string | null;
  /** The map chosen, its id as JSON, saved at once; whether it was kept. */
  onChange: (value: string) => Promise<boolean>;
  /** Whose map it is, as the World's banner says while choosing on it. */
  who: string;
  label: string;
}

/** The world coordinates typed are on: the Twelve's, where nearly every map is. */
const TWELVE = 1;

function chosenId(json: string | null): number | null {
  if (json === null) {
    return null;
  }
  try {
    const value: unknown = JSON.parse(json);
    return typeof value === "number" ? value : null;
  } catch {
    return null;
  }
}

/** Coordinates typed "5,-18", or `null` for text that holds none. */
function coordinates(text: string): [number, number] | null {
  const match = /^\s*(-?\d+)\s*,\s*(-?\d+)\s*$/.exec(text);
  return match === null ? null : [Number(match[1]), Number(match[2])];
}

/**
 * A map (the owner, 2026-11-17): the one chosen shown by its coordinates and sub-area; its target opens the World tab
 * choosing a map, a click there taking the one the world map draws; coordinates typed "5,-18" take the Twelve's; and
 * "Where is <character>" takes the map a character online stands on, an inside too, which shares its street's
 * coordinates.
 */
export function MapField({ value, onChange, who, label }: MapFieldProps) {
  const { t, i18n } = useTranslation();
  const id = chosenId(value);
  const [chosen, setChosen] = useState<MapView_Serialize | null>(null);
  useEffect(() => {
    if (id === null) {
      setChosen(null);
      return;
    }
    let live = true;
    commands
      .findMaps([id])
      .then((found) => live && setChosen(found[0] ?? null))
      // Without the game data, the map is said by its id.
      .catch(() => live && setChosen(null));
    return () => {
      live = false;
    };
  }, [id]);
  // The latest `onChange`, for a choice on the World that ends after renders with another.
  const latest = useRef(onChange);
  useEffect(() => {
    latest.current = onChange;
  }, [onChange]);
  const [draft, setDraft] = useState("");
  const [wrong, setWrong] = useState(false);
  const typed = () => {
    const at = coordinates(draft);
    if (draft.trim() === "") {
      setWrong(false);
      return;
    }
    if (at === null) {
      setWrong(true);
      return;
    }
    commands
      .mapAt(TWELVE, at[0], at[1])
      .then((map) => {
        setWrong(map === null);
        if (map !== null) {
          setDraft("");
          void onChange(String(map.id));
        }
      })
      .catch(() => setWrong(true));
  };
  const views = useCharacters((store) => store.views);
  const standing = Object.values(views).flatMap((view) =>
    view.name != null && view.place?.mapId != null ? [{ name: view.name, map: view.place.mapId }] : [],
  );
  const shown =
    chosen === null
      ? id === null
        ? t("library.value.none")
        : t("library.value.map", { id })
      : t("values.mapAt", {
          x: chosen.at[0],
          y: chosen.at[1],
          subArea: named(chosen.subArea, i18n.language) ?? "",
        });
  return (
    <div className="flex flex-col gap-1 text-xs">
      <span className="flex h-[22px] items-center gap-1.5 text-white">
        <MapPin aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted shrink-0" />
        <span className="truncate">{shown}</span>
        <button
          type="button"
          aria-label={t("values.chooseOnWorld", { label })}
          title={t("values.chooseOnWorld", { label })}
          onClick={() => {
            useWorld.getState().startChoosing(who, (map) => void latest.current(String(map)), "map");
            openWorld();
          }}
          className="text-text-muted hover:bg-hover focus-visible:outline-focus ml-auto flex size-5 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
        >
          <Crosshair aria-hidden="true" size={14} strokeWidth={1.5} />
        </button>
      </span>
      <input
        type="text"
        aria-label={t("values.mapTyped", { label })}
        aria-invalid={wrong}
        placeholder="5,-18"
        value={draft}
        onChange={(event) => {
          setDraft(event.target.value);
          setWrong(false);
        }}
        onBlur={typed}
        onKeyDown={(event) => event.key === "Enter" && typed()}
        className="focus:border-focus h-[26px] min-w-0 rounded border border-[#3c3c3c] bg-[#181818] px-2 text-white outline-0 aria-invalid:border-[#f48771]"
      />
      {standing.map((one) => (
        <button
          key={one.name}
          type="button"
          onClick={() => void onChange(String(one.map))}
          className="hover:bg-hover focus-visible:outline-focus self-start rounded px-1 py-0.5 text-left focus-visible:outline-1"
        >
          {t("values.whereIs", { name: one.name })}
        </button>
      ))}
    </div>
  );
}
