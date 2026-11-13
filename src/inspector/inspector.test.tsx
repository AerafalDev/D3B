import { act, cleanup, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { GameConnectionStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { useLayout } from "@/shell/layout-store.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { Inspector } from "./inspector.tsx";
import { InspectorCounts } from "./inspector-counts.tsx";
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
    <>
      <InspectorCounts />
      <Inspector />
    </>,
  );
}

describe("the inspector", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: false } });
    useEditors.setState({ active: "character:1" });
    useInspector.setState({ connection: 1, frames: [frame] });
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
});
