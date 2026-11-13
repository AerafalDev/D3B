import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { useRelay } from "@/relay/relay-store.ts";
import { FrameDetail } from "./frame-detail.tsx";
import { FrameList } from "./frame-list.tsx";
import { type FrameSummary, useInspector } from "./inspector-store.ts";
import { useWatchedConnection } from "./use-watched-connection.ts";

// The same empty list each time, while the store has not watched this connection yet.
const NONE: readonly FrameSummary[] = [];

/**
 * The panel's Protocol view (`docs/protocol.md`, "The inspector", the owner's layout of 2026-11-13): the frames of the
 * character whose tab is active, newest last, filtered by side and name, and the chosen one's detail on their right. Without such a tab it says what to do; for a connection whose protocol
 * is not read, or closed, only that it keeps nothing: the character's tab says why, and nothing is said twice.
 */
export function Inspector() {
  const { t } = useTranslation();
  const connection = useWatchedConnection();
  const watched = useInspector((store) => store.connection);
  const frames = useInspector((store) => store.frames);
  const selected = useInspector((store) => store.selected);
  const select = useInspector((store) => store.select);
  const side = useInspector((store) => store.side);
  const name = useInspector((store) => store.name);
  // A frame shows when its side is shown and its readable name, or the build's, holds the filter's text.
  const shows = useMemo(() => {
    const text = name.trim().toLowerCase();
    if (side === "all" && text === "") {
      return undefined;
    }
    return (frame: FrameSummary) =>
      (side === "all" || frame.fromClient === (side === "client")) &&
      [frame.readable, frame.name].some((one) => one?.toLowerCase().includes(text) ?? false);
  }, [side, name]);
  const protocol = useRelay((store) => store.status?.connections.find((one) => one.number === connection)?.protocol);

  if (connection === null) {
    return <Notice text={t("inspector.none")} />;
  }
  if (protocol === undefined || protocol === "unmapped" || protocol === "unknownBuild") {
    return <Notice text={t("inspector.nothing")} />;
  }
  // Until the store watches this tab's connection, its frames are another's; a list of its own per connection starts
  // following its newest frames.
  return (
    <div className="flex min-h-0 flex-1">
      <FrameList
        key={connection}
        frames={watched === connection ? frames : NONE}
        shows={shows}
        selected={selected}
        onSelect={select}
      />
      <FrameDetail />
    </div>
  );
}

function Notice({ text }: { text: string }) {
  return (
    <p className="text-text-muted flex flex-1 items-center justify-center px-6 pb-[35px] text-center text-xs">{text}</p>
  );
}
