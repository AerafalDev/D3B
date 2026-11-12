import { useTranslation } from "react-i18next";
import { Kbd } from "@/components/ui/kbd.tsx";
import appIcon from "../../../src-tauri/icons/128x128.png";
import type { CommandId } from "../commands.ts";
import { useShortcut } from "../use-shortcut.ts";

const SHOWN: readonly CommandId[] = ["palette", "primarySideBar", "panel", "secondarySideBar"];

/**
 * What the editor area shows with no editor open, as VS Code's: the app's mark in relief and the main shortcuts, these
 * only when the area is wide enough for them.
 */
export function EditorWatermark() {
  const { t } = useTranslation();
  const shortcut = useShortcut();

  return (
    <div className="@container flex h-full flex-col items-center justify-center gap-8 overflow-hidden select-none">
      <img src={appIcon} alt="" draggable={false} className="size-32 opacity-[0.12] grayscale" />
      <dl className="hidden grid-cols-[auto_auto] items-center gap-x-4 gap-y-2.5 text-[13px] @[480px]:grid">
        {SHOWN.map((id) => (
          <div key={id} className="contents">
            <dt className="text-text-muted text-right whitespace-nowrap">{t(`shell.commands.${id}`)}</dt>
            <dd>
              <Kbd className="text-text-muted h-5 rounded-[3px] bg-white/[0.06] px-1.5 font-sans text-[11px] shadow-[inset_0_-1px_0_rgb(255_255_255/0.08)]">
                {shortcut(id)}
              </Kbd>
            </dd>
          </div>
        ))}
      </dl>
    </div>
  );
}
