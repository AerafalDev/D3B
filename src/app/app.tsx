import { useEffect } from "react";

import { useCharacters } from "@/characters/character-store.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { Shell } from "@/shell/shell.tsx";
import { useAutoUpdate } from "@/updates/use-auto-update.ts";

/**
 * The app: its shell; the relay and the characters followed from the back end for the whole session; the updates that
 * download at start and install on close.
 */
export function App() {
  useAutoUpdate();
  useEffect(() => {
    const stopRelay = useRelay.getState().follow();
    const stopCharacters = useCharacters.getState().follow();
    return () => {
      stopRelay();
      stopCharacters();
    };
  }, []);
  return <Shell />;
}
