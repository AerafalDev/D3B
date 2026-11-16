import { useEffect } from "react";

import { useCharacters } from "@/characters/character-store.ts";
import { usePlayed } from "@/characters/played/played-store.ts";
import { useInspector } from "@/inspector/inspector-store.ts";
import { useLibrary } from "@/library/library-store.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { useAppSettings } from "@/settings/app-settings.ts";
import { Shell } from "@/shell/shell.tsx";
import { useAutoUpdate } from "@/updates/use-auto-update.ts";

/**
 * The app: its shell and its settings, read at start; the relay, the characters, those played, the Library and the
 * inspector's frames followed from the back end for the whole session, the frames even with Advanced tools off, so
 * that turning them on shows what came before (the owner, 2026-11-16); the updates that download at start and
 * install on close.
 */
export function App() {
  useAutoUpdate();
  useEffect(() => {
    void useAppSettings.getState().load();
    const stopRelay = useRelay.getState().follow();
    const stopCharacters = useCharacters.getState().follow();
    const stopPlayed = usePlayed.getState().follow();
    const stopInspector = useInspector.getState().follow();
    const stopLibrary = useLibrary.getState().follow();
    return () => {
      stopInspector();
      stopRelay();
      stopCharacters();
      stopPlayed();
      stopLibrary();
    };
  }, []);
  return <Shell />;
}
