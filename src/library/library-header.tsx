import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { useShare } from "@/share/share-store.ts";
import { IconButton } from "@/shell/icon-button.tsx";

/** The Library's actions in the side bar's header: Import…, a `.d3b` chosen in the system's open dialog. */
export function LibraryHeader() {
  const { t } = useTranslation();
  const pick = useShare((store) => store.pick);
  return (
    <div className="ml-auto flex items-center">
      <IconButton label={t("share.import")} onClick={() => void pick()}>
        <Icon name="fileImport" />
      </IconButton>
    </div>
  );
}
