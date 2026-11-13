import type { TFunction } from "i18next";

import type { RelayStatus } from "@/bindings.ts";
import type { StatusHover } from "@/shell/status-bar/status-item.tsx";

/** The relay's item in the status bar: its state, and once ready the game connections it relays. */
export function relayLabel(status: RelayStatus, t: TFunction): string {
  return status.state === "ready" ? t("relay.ready", { count: status.gameConnections }) : t(`relay.${status.state}`);
}

/**
 * What the player needs to know of the relay and the bar does not say (the owner, 2026-11-13): why it is off, the
 * client's build only when the game data is not of it, the answers not understood; then that quitting ends the game
 * connections and to start Dofus after D3B. What the protocol reads of each connection is in the characters' views
 * and the inspector, not here.
 */
export function relayHover(status: RelayStatus, dataBuild: string | null, t: TFunction): StatusHover {
  const facts: string[] = [];
  if (status.reason) {
    facts.push(status.reason);
  }
  const client = status.clientBuild;
  if (client && dataBuild === null) {
    facts.push(t("relay.client", { build: client }));
  } else if (client && client !== dataBuild) {
    facts.push(t("relay.clientOther", { build: client, data: dataBuild }));
  }
  if (status.notUnderstood > 0) {
    facts.push(t("relay.notUnderstood", { count: status.notUnderstood }));
  }
  const advice: string[] = [];
  if (status.gameConnections > 0) {
    advice.push(t("relay.quitEnds"));
  }
  if (status.state === "ready") {
    advice.push(t("relay.startAfter"));
  }
  return [facts, advice].filter((section) => section.length > 0);
}
