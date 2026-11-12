import { useAutoUpdate } from "@/updates/use-auto-update.ts";

/**
 * The app's root, empty while the studio's shell is rebuilt in VS Code's layout (docs/now.md). Updates still download
 * and install on close.
 */
export function App() {
  useAutoUpdate();
  return <div className="bg-background text-foreground h-svh" />;
}
