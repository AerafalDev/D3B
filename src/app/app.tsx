import { Shell } from "@/shell/shell.tsx";
import { useAutoUpdate } from "@/updates/use-auto-update.ts";

/** The app: its shell, and the updates that download at start and install on close. */
export function App() {
  useAutoUpdate();
  return <Shell />;
}
