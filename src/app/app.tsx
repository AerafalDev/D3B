import { useTranslation } from "react-i18next";

import { CharacterList } from "@/characters/character-list.tsx";
import { CharacterView } from "@/characters/character-view.tsx";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable.tsx";
import { UpdateBanner } from "@/updates/update-banner.tsx";
import { useAutoUpdate } from "@/updates/use-auto-update.ts";
import { StatusBar } from "./status-bar.tsx";

/** The app's shell: the characters on the left, the workspace in the middle, the status bar below. */
export function App() {
  const { t } = useTranslation();
  useAutoUpdate();
  return (
    <div className="bg-background text-foreground flex h-svh flex-col">
      <header className="flex h-9 shrink-0 items-center border-b px-3 text-sm font-medium">{t("app.name")}</header>
      <UpdateBanner />
      <ResizablePanelGroup orientation="horizontal" className="min-h-0 flex-1">
        <ResizablePanel defaultSize="20%" minSize={220} className="flex flex-col">
          <div className="text-muted-foreground border-b px-3 py-2 text-xs font-medium tracking-wide uppercase">
            {t("characters.title")}
          </div>
          <CharacterList />
        </ResizablePanel>
        <ResizableHandle />
        <ResizablePanel defaultSize="80%" className="flex flex-col">
          <CharacterView />
        </ResizablePanel>
      </ResizablePanelGroup>
      <StatusBar />
    </div>
  );
}
