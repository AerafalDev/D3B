import { useTranslation } from "react-i18next";

import { Icon } from "@/icons/icon.tsx";
import { useRelay } from "@/relay/relay-store.ts";
import type { StatusHover } from "@/shell/status-bar/status-item.tsx";
import { StatusItem } from "@/shell/status-bar/status-item.tsx";
import { useUpdater } from "./updater.ts";

// The notes a release may carry, cut so that the hover stays a hover.
const NOTES_SHOWN = 6;

/**
 * The offer to restart into a downloaded update, on the status bar's right (the owner's "3a", 2026-11-13): JetBrains'
 * orange update icon and a bright label; chosen, it installs and restarts at once. Its hover adds only what the label
 * does not say: the release's notes, and that restarting ends the game connections while some are open.
 */
export function UpdateItem() {
  const { t } = useTranslation();
  const state = useUpdater((store) => store.state);
  const restart = useUpdater((store) => store.restart);
  const connections = useRelay((store) => store.status?.gameConnections ?? 0);

  if (state.status === "installing") {
    return (
      <StatusItem
        lead={<Icon name="ideUpdate" className="size-3.5" />}
        label={t("updates.installing", { version: state.version })}
        onClick={() => void restart()}
        disabled
        className="text-text"
      />
    );
  }
  if (state.status !== "ready") {
    return null;
  }
  const notes = (state.notes ?? "")
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .slice(0, NOTES_SHOWN);
  const hover: StatusHover = [notes, connections > 0 ? [t("updates.endsGame")] : []].filter(
    (section) => section.length > 0,
  );
  return (
    <StatusItem
      lead={<Icon name="ideUpdate" className="size-3.5" />}
      label={t("updates.restart", { version: state.version })}
      hover={hover}
      onClick={() => void restart()}
      className="text-text"
    />
  );
}
