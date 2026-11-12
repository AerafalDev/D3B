import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { IconButton } from "../icon-button.tsx";
import { type Part, useLayout } from "../layout-store.ts";
import { useShortcut } from "../use-shortcut.ts";

interface PartFrameProps {
  part: Part;
  /** The view it shows, named in its header; without one, it says its views come with the features. */
  title?: string;
  children?: ReactNode;
}

/**
 * A side bar's or the panel's frame: its 35 px header with its view's name and the button that hides it, then its
 * view; before any view, a line saying the views come with the features.
 */
export function PartFrame({ part, title, children }: PartFrameProps) {
  const { t } = useTranslation();
  const toggle = useLayout((store) => store.toggle);
  const shortcut = useShortcut();
  const name = t(`shell.parts.${part}`);

  return (
    <section aria-label={name} className="flex min-h-0 flex-1 flex-col">
      <div className="flex h-[35px] shrink-0 items-center gap-2 pr-2 pl-3">
        {title !== undefined && <h2 className="text-text-muted truncate text-xs">{title}</h2>}
        <span className="flex-1" />
        <IconButton label={t(`shell.hide.${part}`)} shortcut={shortcut(part)} onClick={() => toggle(part)}>
          <Icon name="close" />
        </IconButton>
      </div>
      {children ?? (
        <p className="text-text-muted flex flex-1 items-center justify-center px-6 pb-[35px] text-center text-xs">
          {t("shell.empty")}
        </p>
      )}
    </section>
  );
}
