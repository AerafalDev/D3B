import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { useLayout } from "./layout-store.ts";
import { useShellShortcuts } from "./use-shell-shortcuts.ts";

function Shortcuts() {
  useShellShortcuts();
  return <input aria-label="field" />;
}

const start = { primarySideBar: true, panel: true, secondarySideBar: false };

describe("the shell's shortcuts", () => {
  beforeEach(() => useLayout.setState({ visible: { ...start } }));
  afterEach(cleanup);

  it("toggle each part with VS Code's keys", () => {
    render(<Shortcuts />);
    fireEvent.keyDown(document, { key: "b", code: "KeyB", ctrlKey: true });
    fireEvent.keyDown(document, { key: "j", code: "KeyJ", ctrlKey: true });
    fireEvent.keyDown(document, { key: "b", code: "KeyB", ctrlKey: true, altKey: true });
    expect(useLayout.getState().visible).toEqual({ primarySideBar: false, panel: false, secondarySideBar: true });
  });

  it("work while a field has the focus", () => {
    render(<Shortcuts />);
    fireEvent.keyDown(screen.getByLabelText("field"), { key: "j", code: "KeyJ", ctrlKey: true });
    expect(useLayout.getState().visible.panel).toBe(false);
  });

  it("take the key from the webview, so Ctrl+J or Ctrl+P does nothing else", () => {
    render(<Shortcuts />);
    const delivered = fireEvent.keyDown(document, { key: "j", code: "KeyJ", ctrlKey: true });
    expect(delivered).toBe(false);
  });

  it("leave a key without its modifier alone", () => {
    render(<Shortcuts />);
    const delivered = fireEvent.keyDown(document, { key: "b", code: "KeyB" });
    expect(delivered).toBe(true);
    expect(useLayout.getState().visible).toEqual(start);
  });
});
