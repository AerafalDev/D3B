import { act, cleanup, render } from "@testing-library/react";
import type { ReactNode, Ref } from "react";
import { afterEach, beforeEach, describe, expect, it, type Mock, vi } from "vitest";

// The library lays out nothing in happy-dom: a fake panel per part records what the part asks of it.
interface FakePanel {
  collapsed: boolean;
  collapse: Mock<() => void>;
  expand: Mock<() => void>;
  resize: Mock<(size: number | string) => void>;
  onResize: (() => void) | null;
}

const panels = vi.hoisted(() => new Map<string, FakePanel>());

function fake(id: string, defaultSize = 1): FakePanel {
  let found = panels.get(id);
  if (found === undefined) {
    // As the library: a part laid out at its collapsed size, 0, starts collapsed.
    found = { collapsed: defaultSize === 0, collapse: vi.fn(), expand: vi.fn(), resize: vi.fn(), onResize: null };
    panels.set(id, found);
  }
  return found;
}

vi.mock("@/components/ui/resizable.tsx", () => ({
  ResizablePanel: ({
    id,
    defaultSize,
    panelRef,
    elementRef,
    onResize,
    children,
  }: {
    id: string;
    defaultSize: number;
    panelRef: { current: unknown };
    elementRef: Ref<HTMLDivElement>;
    onResize: () => void;
    children: ReactNode;
  }) => {
    const panel = fake(id, defaultSize);
    panel.onResize = onResize;
    panelRef.current = {
      isCollapsed: () => panel.collapsed,
      collapse: () => {
        panel.collapse();
        panel.collapsed = true;
      },
      expand: () => {
        panel.expand();
        panel.collapsed = false;
      },
      resize: (size: number | string) => {
        panel.resize(size);
        panel.collapsed = false;
      },
    };
    return <div ref={elementRef}>{children}</div>;
  },
}));

const { ResizablePart, ANIMATION_MS } = await import("./resizable-part.tsx");
const { useLayout } = await import("../layout-store.ts");

function renderParts() {
  return render(
    <div data-testid="group">
      <ResizablePart part="primarySideBar" defaultSize={300} minSize={170}>
        <p>left</p>
      </ResizablePart>
      <ResizablePart part="secondarySideBar" defaultSize={300} minSize={170}>
        <p>right</p>
      </ResizablePart>
    </div>,
  );
}

const group = () => document.querySelector("[data-testid=group]");

describe("a resizable part", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    panels.clear();
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: false } });
  });
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("starts laid out collapsed when it starts hidden, without sliding", () => {
    renderParts();
    expect(fake("secondarySideBar").collapsed).toBe(true);
    expect(fake("primarySideBar").collapsed).toBe(false);
    expect(group()?.hasAttribute("data-animating")).toBe(false);
  });

  it("opens at its default size the first time it is shown after starting hidden", () => {
    renderParts();
    act(() => useLayout.getState().setVisible("secondarySideBar", true));
    expect(fake("secondarySideBar").resize).toHaveBeenCalledWith(300);
  });

  it("collapses when the layout hides it, and expands back to its size when it shows it", () => {
    renderParts();
    act(() => useLayout.getState().setVisible("primarySideBar", false));
    expect(fake("primarySideBar").collapse).toHaveBeenCalledOnce();
    act(() => useLayout.getState().setVisible("primarySideBar", true));
    expect(fake("primarySideBar").expand).toHaveBeenCalledOnce();
  });

  it("slides its group for 200 ms when a command shows it", () => {
    renderParts();
    act(() => useLayout.getState().setVisible("secondarySideBar", true));
    expect(group()?.hasAttribute("data-animating")).toBe(true);
    act(() => vi.advanceTimersByTime(ANIMATION_MS));
    expect(group()?.hasAttribute("data-animating")).toBe(false);
  });

  it("keeps its group sliding until the last of two quick toggles ends", () => {
    renderParts();
    act(() => useLayout.getState().setVisible("primarySideBar", false));
    act(() => vi.advanceTimersByTime(150));
    act(() => useLayout.getState().setVisible("secondarySideBar", true));
    act(() => vi.advanceTimersByTime(60));
    expect(group()?.hasAttribute("data-animating")).toBe(true);
    act(() => vi.advanceTimersByTime(ANIMATION_MS));
    expect(group()?.hasAttribute("data-animating")).toBe(false);
  });

  it("hides itself in the layout once dragged below its minimum", () => {
    renderParts();
    fake("primarySideBar").collapsed = true;
    act(() => fake("primarySideBar").onResize?.());
    expect(useLayout.getState().visible.primarySideBar).toBe(false);
  });
});
