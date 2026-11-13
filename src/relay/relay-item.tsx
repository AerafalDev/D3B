import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { StatusItem } from "@/shell/status-bar/status-item.tsx";
import { relayHover, relayLabel } from "./relay-hover.ts";
import { useRelay } from "./relay-store.ts";

interface RelayItemProps {
  /** The game data's build, so that the hover names the client's only when it differs. */
  dataBuild: string | null;
}

/**
 * The relay in the status bar (the owner, 2026-11-13): JetBrains' arrows both ways and its state, grey; red with
 * JetBrains' error once off. Nothing before the back end tells its status, and in the browser preview.
 */
export function RelayItem({ dataBuild }: RelayItemProps) {
  const { t } = useTranslation();
  const status = useRelay((store) => store.status);
  if (status === null) {
    return null;
  }
  const failed = status.state === "failed";
  return (
    <StatusItem
      lead={<Icon name={failed ? "error" : "arrowLeftRight"} className="size-3.5" />}
      label={relayLabel(status, t)}
      hover={relayHover(status, dataBuild, t)}
      className={failed ? "text-error" : undefined}
    />
  );
}
