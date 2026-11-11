import { Users } from "lucide-react";
import { useTranslation } from "react-i18next";

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
          <div className="text-muted-foreground flex flex-1 flex-col items-center justify-center gap-2 p-4 text-center text-sm">
            <Users className="size-6" />
            <p>{t("characters.empty")}</p>
          </div>
        </ResizablePanel>
        <ResizableHandle />
        <ResizablePanel defaultSize="80%" className="text-muted-foreground flex items-center justify-center text-sm">
          {t("workspace.empty")}
        </ResizablePanel>
      </ResizablePanelGroup>
      <StatusBar />
    </div>
  );
}
