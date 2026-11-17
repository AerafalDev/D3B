import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import type { GameConnectionStatus } from "@/bindings.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { useAppSettings } from "@/settings/app-settings.ts";
import { useLayout } from "@/shell/layout-store.ts";
import { useEditors } from "@/shell/parts/editor-store.ts";
import { revealProtocolPanel, useProtocolPanel } from "@/shell/protocol-panel.ts";
import { usePanelOpening } from "./use-panel-opening.ts";

// Only the connection's number plays a part: the panel opens for a character whose client is connected.
function connected(...numbers: number[]) {
  const connections = numbers.map((number) => ({ number }) as GameConnectionStatus);
  useRelay.setState({
    status: {
      state: "ready",
      reason: null,
      clientBuild: null,
      gameConnections: connections.length,
      notUnderstood: 0,
      connections,
    },
  });
}

function activate(editor: string | null) {
  act(() => useEditors.setState({ active: editor }));
}

function panel(): boolean {
  return useLayout.getState().visible.panel;
}

describe("the Protocol panel's opening", () => {
  beforeEach(() => {
    useAppSettings.setState({ advancedTools: true, changed: false });
    useLayout.setState({ visible: { primarySideBar: true, panel: false, secondarySideBar: false } });
    useEditors.setState({ active: null });
    useProtocolPanel.setState({ dismissed: false });
    connected(1, 2);
  });
  afterEach(cleanup);

  it("stays closed at start, with no character's tab active", () => {
    renderHook(() => usePanelOpening());
    expect(panel()).toBe(false);
  });

  it("opens when a connected character's tab becomes active", () => {
    renderHook(() => usePanelOpening());
    activate("character:1");
    expect(panel()).toBe(true);
  });

  it("stays closed without Advanced tools, where it has no view", () => {
    act(() => useAppSettings.setState({ advancedTools: false }));
    renderHook(() => usePanelOpening());
    activate("character:1");
    expect(panel()).toBe(false);
  });

  it("stays closed for a character whose client is closed, another editor, or none", () => {
    renderHook(() => usePanelOpening());
    activate("character:3");
    activate("played:294-1");
    activate("blueprint:7");
    expect(panel()).toBe(false);
  });

  it("stays closed once closed by hand, as other characters' tabs become active", () => {
    renderHook(() => usePanelOpening());
    activate("character:1");
    act(() => useLayout.getState().toggle("panel"));
    activate("character:2");
    activate("blueprint:7");
    activate("character:1");
    expect(panel()).toBe(false);
  });

  it("reopens at a click on a character online, after a close by hand", () => {
    renderHook(() => usePanelOpening());
    activate("character:1");
    act(() => useLayout.getState().toggle("panel"));
    act(() => revealProtocolPanel());
    expect(panel()).toBe(true);
    // Open again, it follows the tabs again: a later close by hand is the one that counts.
    act(() => useLayout.getState().toggle("panel"));
    activate("character:2");
    expect(panel()).toBe(false);
  });

  it("does not open at a click without Advanced tools", () => {
    act(() => useAppSettings.setState({ advancedTools: false }));
    act(() => revealProtocolPanel());
    expect(panel()).toBe(false);
  });

  it("opens as Advanced tools turn on while a connected character's tab is active, and not otherwise", () => {
    act(() => useAppSettings.getState().setAdvancedTools(false));
    renderHook(() => usePanelOpening());
    activate("blueprint:7");
    act(() => useAppSettings.getState().setAdvancedTools(true));
    expect(panel()).toBe(false);
    act(() => useAppSettings.getState().setAdvancedTools(false));
    activate("character:2");
    act(() => useAppSettings.getState().setAdvancedTools(true));
    expect(panel()).toBe(true);
  });

  it("forgets a close by hand once Advanced tools turn off, which is not one", () => {
    renderHook(() => usePanelOpening());
    activate("character:1");
    act(() => useLayout.getState().toggle("panel"));
    act(() => useAppSettings.getState().setAdvancedTools(false));
    act(() => useAppSettings.getState().setAdvancedTools(true));
    expect(panel()).toBe(true);
  });

  it("opens when the active character's client connects", () => {
    connected();
    renderHook(() => usePanelOpening());
    activate("character:1");
    expect(panel()).toBe(false);
    act(() => connected(1));
    expect(panel()).toBe(true);
  });
});
