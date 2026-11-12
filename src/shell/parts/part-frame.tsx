import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { IconButton } from "../icon-button.tsx";
import { type Part, useLayout } from "../layout-store.ts";
import { useShortcut } from "../use-shortcut.ts";

interface PartFrameProps {
  part: Part;
}

/**
 * A side bar's or the panel's frame before any view: its 35 px header with the button that hides it, and a line
 * saying the views come with the features.
 */
export function PartFrame({ part }: PartFrameProps) {
  const { t } = useTranslation();
  const toggle = useLayout((store) => store.toggle);
  const shortcut = useShortcut();
  const name = t(`shell.parts.${part}`);

  return (
    <section aria-label={name} className="flex min-h-0 flex-1 flex-col">
      <div className="flex h-[35px] shrink-0 items-center justify-end px-2">
        <IconButton label={t(`shell.hide.${part}`)} shortcut={shortcut(part)} onClick={() => toggle(part)}>
          <Icon name="close" />
        </IconButton>
      </div>
      <p className="text-text-muted flex flex-1 items-center justify-center px-6 pb-[35px] text-center text-xs">
        {t("shell.empty")}
      </p>
    </section>
  );
}
