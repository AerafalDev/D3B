import type { IDockviewPanelHeaderProps } from "dockview-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import type { EditorParams } from "./editor-store.ts";

/**
 * An editor's tab as the owner chose it (2026-11-12, from mock-ups): its icon, its title and its close button; the
 * active one rises from the strip with rounded top corners on the editor's ground and merges into it, without a blue
 * line. The close button shows on the active tab or under the pointer.
 */
export function EditorTab({ api, params }: IDockviewPanelHeaderProps<EditorParams>) {
  const { t } = useTranslation();
  const [title, setTitle] = useState(api.title ?? "");

  useEffect(() => {
    const listening = api.onDidTitleChange((event) => setTitle(event.title));
    return () => listening.dispose();
  }, [api]);

  return (
    <div className="group in-[.dv-active-tab]:bg-editor hover:text-text-muted flex h-full items-center gap-1.5 rounded-t-[6px] pr-1.5 pl-3 text-[13px] text-[#8a8a8a] hover:bg-[#181818] in-[.dv-active-tab]:text-white">
      <Icon name={params.icon} />
      <span className="truncate">{title}</span>
      <button
        type="button"
        aria-label={t("shell.closeTab", { title })}
        onPointerDown={(event) => event.stopPropagation()}
        onClick={(event) => {
          event.stopPropagation();
          api.close();
        }}
        className="hover:bg-pressed flex size-[18px] items-center justify-center rounded-[4px] opacity-0 group-hover:opacity-100 focus-visible:opacity-100 in-[.dv-active-tab]:opacity-100"
      >
        <Icon name="close" className="size-3.5" />
      </button>
    </div>
  );
}
