import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { GameConnectionStatus } from "@/bindings.ts";
import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { useLayout } from "@/shell/layout-store.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { Inspector } from "./inspector.tsx";
import { InspectorHeader } from "./inspector-header.tsx";
import { type FrameSummary, useInspector } from "./inspector-store.ts";

function connection(change: Partial<GameConnectionStatus> = {}): GameConnectionStatus {
  return {
    number: 1,
    build: "3.7.13.17",
    protocol: "on",
    understood: 12_418,
    read: 3902,
    unknown: 25_510,
    notTrusted: 0,
    dropped: 0,
    map: null,
    ...change,
  };
}

function relay(connections: GameConnectionStatus[]) {
  useRelay.setState({
    status: { state: "ready", reason: null, clientBuild: null, gameConnections: 1, notUnderstood: 0, connections },
  });
}

const frame: FrameSummary = {
  number: 7,
  millis: 0,
  fromClient: false,
  name: "jow",
  readable: "MapCurrentEvent",
  outcome: "understood",
  size: 11,
};

/** A count in the header, by its label: its number is apart, in bold. */
function count(label: string) {
  return screen.queryByText(label, { selector: "span:has(> b)" });
}

function renderPanel() {
  return render(
    <TooltipProvider>
      <InspectorHeader />
      <Inspector />
    </TooltipProvider>,
  );
}

function detail(): string {
  return screen.getByRole("region", { name: "Detail" }).textContent ?? "";
}

function listed(): string {
  return screen.getByRole("list", { name: "Frames" }).textContent ?? "";
}

const unpinned: FrameSummary = {
  ...frame,
  number: 8,
  fromClient: true,
  name: "lok",
  readable: null,
  outcome: "unknown",
};

describe("the inspector", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: false } });
    useEditors.setState({ active: "character:1" });
    useInspector.setState({ connection: 1, frames: [frame], selected: null, detail: null, side: "all", name: "" });
    useInspector.getState().pause(false);
    relay([connection()]);
  });
  afterEach(cleanup);

  it("lists the frames of the character whose tab is active, under its counts", () => {
    renderPanel();

    expect(screen.getByRole("list", { name: "Frames" }).textContent).toContain("MapCurrentEvent");
    expect(count("understood")?.textContent).toBe("understood 12,418");
    expect(count("dropped")?.textContent).toBe("dropped 0");
  });

  it("leaves the count of frames not trusted grey at 0", () => {
    renderPanel();

    expect(count("not trusted")?.className).not.toContain("text-error");
  });

  it("marks the frames not trusted, and them alone, as a problem", () => {
    relay([connection({ notTrusted: 2 })]);
    renderPanel();

    expect(count("not trusted")?.className).toContain("text-error");
    expect(count("unknown")?.className).not.toContain("text-error");
  });

  it("shows no frames of another connection while this tab's are on the way", () => {
    useEditors.setState({ active: "character:2" });
    relay([connection(), connection({ number: 2 })]);
    // The watch asked for, held: the store still lists connection 1's frames when the panel draws tab 2.
    const watch = vi.fn();
    const real = useInspector.getState().watch;
    useInspector.setState({ connection: 1, frames: [frame], watch });
    renderPanel();

    expect(screen.getByRole("list", { name: "Frames" }).textContent).toBe("");
    expect(watch).toHaveBeenCalledWith(2);
    useInspector.setState({ watch: real });
  });

  it("watches the active tab again once the store was reset under it", () => {
    renderPanel();

    act(() => useInspector.setState({ connection: null, frames: [] }));

    expect(useInspector.getState().connection).toBe(1);
  });

  it("asks for a character while no character's tab is active, and watches none", () => {
    useEditors.setState({ active: null });
    renderPanel();

    expect(screen.getByText("Open a character to follow its protocol.")).toBeDefined();
    expect(count("understood")).toBeNull();
    expect(useInspector.getState().connection).toBeNull();
  });

  it("watches none while the panel is hidden", () => {
    useLayout.setState({ visible: { primarySideBar: true, panel: false, secondarySideBar: false } });
    renderPanel();

    expect(useInspector.getState().connection).toBeNull();
  });

  it("keeps nothing for a connection whose protocol is not read, without saying why again, nor counts", () => {
    relay([connection({ protocol: "unmapped", understood: 0, read: 0, unknown: 0 })]);
    renderPanel();

    expect(screen.getByText("No frames kept.")).toBeDefined();
    expect(count("understood")).toBeNull();
  });

  it("keeps nothing once the connection closed", () => {
    relay([]);
    renderPanel();

    expect(screen.getByText("No frames kept.")).toBeDefined();
    expect(screen.queryByRole("list")).toBeNull();
  });

  it("keeps the frames and counts of a protocol lost", () => {
    relay([connection({ protocol: "lost" })]);
    renderPanel();

    expect(screen.getByRole("list", { name: "Frames" })).toBeDefined();
    expect(count("understood")).not.toBeNull();
  });

  it("details the frame chosen by its fields, without its name again, then the fields we do not read, dimmer", () => {
    useInspector.setState({
      selected: 7,
      detail: {
        kind: "detail",
        lines: [
          { kind: "decoded", text: "map_id: 153092354" },
          { kind: "bytes", number: 2, length: 10 },
        ],
      },
    });
    renderPanel();

    expect(detail()).toBe("map_id: 1530923542: 10 bytes");
    expect(screen.getByText("2: 10 bytes").className).toContain("text-text-faint");
    expect(screen.getByRole("button", { current: true }).textContent).toContain("MapCurrentEvent");
  });

  it("says a secret frame's content is never kept", () => {
    useInspector.setState({
      frames: [{ ...frame, outcome: "secret", name: "koh", readable: null }],
      selected: 7,
      detail: { kind: "detail", lines: [] },
    });
    renderPanel();

    expect(detail()).toBe("A secret frame: neither its content nor its bytes are kept.");
  });

  it("says when the frame chosen is no longer kept", () => {
    useInspector.setState({ selected: 7, detail: { kind: "gone" } });
    renderPanel();

    expect(detail()).toBe("This frame is no longer kept: only the last 3,000 are.");
  });

  it("shows one side's frames, and those whose readable or build name holds the filter", () => {
    useInspector.setState({ frames: [frame, unpinned] });
    renderPanel();

    fireEvent.click(screen.getByRole("button", { name: "↑ Client" }));
    expect(listed()).not.toContain("MapCurrentEvent");
    expect(listed()).toContain("lok");

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    fireEvent.change(screen.getByRole("searchbox", { name: "Filter by name" }), { target: { value: "mapcur" } });
    expect(listed()).toContain("MapCurrentEvent");
    expect(listed()).not.toContain("lok");

    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "Fight" } });
    expect(screen.getByText("No frame matches.")).toBeDefined();
  });

  it("stands still while paused, and says how many frames arrived meanwhile", () => {
    renderPanel();

    fireEvent.click(screen.getByRole("button", { name: "Pause" }));
    act(() => useInspector.setState({ held: [unpinned] }));

    expect(screen.getByRole("button", { name: "Pause" }).getAttribute("aria-pressed")).toBe("true");
    expect(screen.getByText("1 arrived since the pause")).toBeDefined();
    expect(listed()).not.toContain("lok");
  });
});
