import { type UIEvent, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils.ts";
import { COLUMNS, FrameRow } from "./frame-row.tsx";
import type { FrameSummary } from "./inspector-store.ts";

/** Each row's height: fixed, so the rows in view follow from the scroll alone. */
const ROW = 22;
/** Rows drawn beyond those in view, so a quick scroll shows no blank. */
const OVERSCAN = 8;
/** Rows drawn before the list's height is known. */
const FALLBACK = 40;

/** A row of the list: a frame, or how many frames between two were not listed. */
type Item = { kind: "frame"; frame: FrameSummary } | { kind: "gap"; count: number; before: number };

/**
 * The frames with a row wherever their numbers jump: a connection numbers its frames one after another, so a jump is
 * frames the list never had (more in a batch than the back end sends, or forgotten before they were listed).
 */
function listed(frames: FrameSummary[]): Item[] {
  const items: Item[] = [];
  let previous: number | null = null;
  for (const frame of frames) {
    if (previous !== null && frame.number > previous + 1) {
      items.push({ kind: "gap", count: frame.number - previous - 1, before: frame.number });
    }
    items.push({ kind: "frame", frame });
    previous = frame.number;
  }
  return items;
}

/** The number an item stands at: its frame's, or for frames not listed, that of the frame after them. */
function numberOf(item: Item): number {
  return item.kind === "frame" ? item.frame.number : item.before;
}

interface FrameListProps {
  frames: FrameSummary[];
}

/**
 * The connection's frames, newest last, drawn by hand: only the rows in view, at a fixed height. While scrolled to its
 * end, the list follows the frames as they come; scrolled up, it keeps the row at its top in place, though the oldest
 * frames go as new ones come.
 */
export function FrameList({ frames }: FrameListProps) {
  const { t } = useTranslation();
  const box = useRef<HTMLDivElement>(null);
  const before = useRef<HTMLDivElement>(null);
  const after = useRef<HTMLDivElement>(null);
  const [view, setView] = useState({ top: 0, height: 0 });
  const [following, setFollowing] = useState(true);
  // Scrolled up, the row at the top of the view and how far it is scrolled past, kept there as the list changes.
  const anchor = useRef({ number: 0, offset: 0 });

  const items = useMemo(() => listed(frames), [frames]);
  const shown = view.height > 0 ? Math.ceil(view.height / ROW) : FALLBACK;
  const first = following
    ? Math.max(0, items.length - shown - OVERSCAN)
    : Math.min(items.length, Math.max(0, Math.floor(view.top / ROW) - OVERSCAN));
  const last = Math.min(items.length, first + shown + 2 * OVERSCAN);

  // The rows not drawn keep their room, so the scrollbar is the whole list's; set on the elements, not by React.
  useLayoutEffect(() => {
    if (before.current !== null && after.current !== null) {
      before.current.style.height = `${first * ROW}px`;
      after.current.style.height = `${(items.length - last) * ROW}px`;
    }
    if (following && box.current !== null) {
      box.current.scrollTop = box.current.scrollHeight;
    }
  }, [first, last, items.length, following]);

  useLayoutEffect(() => {
    if (following || box.current === null) {
      return;
    }
    const found = items.findIndex((item) => numberOf(item) >= anchor.current.number);
    const top = (found < 0 ? items.length : found) * ROW + anchor.current.offset;
    if (box.current.scrollTop !== top) {
      box.current.scrollTop = top;
    }
  }, [items, following]);

  useEffect(() => {
    const element = box.current;
    if (element === null || typeof ResizeObserver === "undefined") {
      return;
    }
    const observer = new ResizeObserver(() => setView({ top: element.scrollTop, height: element.clientHeight }));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const scrolled = (event: UIEvent<HTMLDivElement>) => {
    const element = event.currentTarget;
    const top = element.scrollTop;
    const item = items[Math.floor(top / ROW)];
    anchor.current = { number: item === undefined ? 0 : numberOf(item), offset: top % ROW };
    setView({ top, height: element.clientHeight });
    setFollowing(top + element.clientHeight >= element.scrollHeight - ROW / 2);
  };

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col">
      <div aria-hidden="true" className={cn(COLUMNS, "text-text-faint h-[22px] shrink-0 px-3 text-[11px]")}>
        <span className="text-right">{t("inspector.columns.number")}</span>
        <span className="pl-2">{t("inspector.columns.time")}</span>
        <span />
        <span>{t("inspector.columns.message")}</span>
        <span>{t("inspector.columns.outcome")}</span>
        <span className="text-right">{t("inspector.columns.size")}</span>
      </div>
      <div ref={box} onScroll={scrolled} className="min-h-0 flex-1 overflow-y-auto">
        <div ref={before} />
        <ol aria-label={t("inspector.frames")}>
          {items.slice(first, last).map((item) =>
            item.kind === "frame" ? (
              <li key={item.frame.number}>
                <FrameRow frame={item.frame} />
              </li>
            ) : (
              <li
                key={`gap-${item.before}`}
                className="text-text-faint h-[22px] text-center text-[11.5px] leading-[22px]"
              >
                {t("inspector.notListed", { count: item.count })}
              </li>
            ),
          )}
        </ol>
        <div ref={after} />
      </div>
    </div>
  );
}
