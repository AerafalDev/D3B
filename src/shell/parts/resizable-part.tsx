import { type ReactNode, useEffect, useRef, useState } from "react";
import { usePanelRef } from "react-resizable-panels";

import { ResizablePanel } from "@/components/ui/resizable.tsx";
import { cn } from "@/lib/utils.ts";
import { type Part, useLayout } from "../layout-store.ts";

/** How long a part takes to open or close (`src/index.css`, `[data-animating]`). */
export const ANIMATION_MS = 200;

// The slides running in each group: a group animates while any of its parts slides, so a second toggle within the
// first one's 200 ms does not cut the first short.
const running = new WeakMap<Element, number>();

function startSlide(group: Element): () => void {
  running.set(group, (running.get(group) ?? 0) + 1);
  group.setAttribute("data-animating", "");
  let ended = false;
  return () => {
    if (ended) {
      return;
    }
    ended = true;
    const left = (running.get(group) ?? 1) - 1;
    running.set(group, left);
    if (left === 0) {
      group.removeAttribute("data-animating");
    }
  };
}

interface ResizablePartProps {
  part: Part;
  /** In pixels. */
  defaultSize: number;
  minSize: number;
  children: ReactNode;
}

/**
 * A part the player resizes and hides. Hidden by a command, it collapses and keeps its size for when it comes back;
 * dragged below its minimum, it hides, as in VS Code. Shown or hidden by a command, it slides open or closed (the
 * owner, 2026-11-12); a drag follows the pointer without animation.
 */
export function ResizablePart({ part, defaultSize, minSize, children }: ResizablePartProps) {
  const ref = usePanelRef();
  const element = useRef<HTMLDivElement | null>(null);
  const visible = useLayout((store) => store.visible[part]);
  const setVisible = useLayout((store) => store.setVisible);
  // A part hidden at start is laid out collapsed from the first frame: it never shows for a frame, and nothing asks the
  // library about it before its group exists. Shown for the first time, it opens at its default size.
  const [startsHidden] = useState(() => !useLayout.getState().visible[part]);
  const shown = useRef(!startsHidden);

  useEffect(() => {
    const panel = ref.current;
    if (panel === null || panel.isCollapsed() !== visible) {
      return;
    }
    const group = element.current?.parentElement ?? null;
    const end = group === null ? null : startSlide(group);
    if (!visible) {
      panel.collapse();
    } else if (shown.current) {
      panel.expand();
    } else {
      shown.current = true;
      panel.resize(defaultSize);
    }
    if (end === null) {
      return;
    }
    const timer = setTimeout(end, ANIMATION_MS);
    return () => {
      clearTimeout(timer);
      end();
    };
  }, [ref, visible, defaultSize]);

  return (
    <ResizablePanel
      id={part}
      panelRef={ref}
      elementRef={element}
      collapsible
      collapsedSize={0}
      defaultSize={startsHidden ? 0 : defaultSize}
      minSize={minSize}
      // As in VS Code, the side bars and the panel keep their size when the window is resized; the editor takes the rest.
      groupResizeBehavior="preserve-pixel-size"
      onResize={() => {
        const collapsed = ref.current?.isCollapsed();
        if (collapsed !== undefined) {
          setVisible(part, !collapsed);
        }
      }}
      // The library sets `overflow: auto` inline: a part narrower than its content for a moment would flash a scrollbar.
      className="flex overflow-hidden!"
    >
      {/* At least its minimum size, so the content slides with the part rather than reflowing as it narrows. */}
      <div
        inert={!visible}
        className={cn(
          "bg-bar border-line flex flex-1 flex-col rounded-[6px] border",
          part === "panel" ? "min-h-[100px]" : "min-w-[170px]",
        )}
      >
        {children}
      </div>
    </ResizablePanel>
  );
}
