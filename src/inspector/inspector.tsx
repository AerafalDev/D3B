import { useTranslation } from "react-i18next";

import { useRelay } from "@/relay/relay-store.ts";
import { FrameList } from "./frame-list.tsx";
import { type FrameSummary, useInspector } from "./inspector-store.ts";
import { useWatchedConnection } from "./use-watched-connection.ts";

// The same empty list each time, while the store has not watched this connection yet.
const NONE: readonly FrameSummary[] = [];

/**
 * The panel's Protocol view (`docs/protocol.md`, "The inspector", the owner's layout of 2026-11-13): the frames of the
 * character whose tab is active, newest last. Without such a tab it says what to do; for a connection whose protocol
 * is not read, or closed, only that it keeps nothing: the character's tab says why, and nothing is said twice.
 */
export function Inspector() {
  const { t } = useTranslation();
  const connection = useWatchedConnection();
  const watched = useInspector((store) => store.connection);
  const frames = useInspector((store) => store.frames);
  const protocol = useRelay((store) => store.status?.connections.find((one) => one.number === connection)?.protocol);

  if (connection === null) {
    return <Notice text={t("inspector.none")} />;
  }
  if (protocol === undefined || protocol === "unmapped" || protocol === "unknownBuild") {
    return <Notice text={t("inspector.nothing")} />;
  }
  // Until the store watches this tab's connection, its frames are another's; a list of its own per connection starts
  // following its newest frames.
  return <FrameList key={connection} frames={watched === connection ? frames : NONE} />;
}

function Notice({ text }: { text: string }) {
  return (
    <p className="text-text-muted flex flex-1 items-center justify-center px-6 pb-[35px] text-center text-xs">{text}</p>
  );
}
