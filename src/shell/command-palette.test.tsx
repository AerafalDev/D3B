import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

vi.mock("@/lib/platform.ts", () => ({ platform: () => "windows" }));
// The browser preview: a setting changed is not saved.
vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => false }));

const { CommandPalette } = await import("./command-palette.tsx");
const { useLayout } = await import("./layout-store.ts");
const { useAppSettings } = await import("@/settings/app-settings.ts");

describe("the command palette", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: true }, paletteOpen: true });
    useAppSettings.setState({ advancedTools: true });
  });
  afterEach(cleanup);

  it("lists the commands with their shortcuts", () => {
    render(<CommandPalette />);
    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Show all commandsCtrl+Shift+P",
      "Toggle the primary side barCtrl+B",
      "Toggle the panelCtrl+J",
      "Toggle the secondary side barCtrl+Alt+B",
      "Toggle advanced tools",
    ]);
  });

  it("leaves the panel's command out without Advanced tools, and turns them on", () => {
    useAppSettings.setState({ advancedTools: false });
    render(<CommandPalette />);
    expect(screen.queryByRole("option", { name: /Toggle the panel/ })).toBeNull();
    fireEvent.click(screen.getByRole("option", { name: "Toggle advanced tools" }));
    expect(useAppSettings.getState().advancedTools).toBe(true);
    expect(useLayout.getState().paletteOpen).toBe(false);
  });

  it("runs the command chosen, then closes", () => {
    render(<CommandPalette />);
    fireEvent.click(screen.getByRole("option", { name: /Toggle the panel/ }));
    expect(useLayout.getState().visible.panel).toBe(false);
    expect(useLayout.getState().paletteOpen).toBe(false);
  });

  it("keeps the commands whose name holds what the player types", () => {
    render(<CommandPalette />);
    fireEvent.change(screen.getByPlaceholderText("Type the name of a command"), { target: { value: "secondary" } });
    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Toggle the secondary side barCtrl+Alt+B",
    ]);
  });

  it("says when no command matches", () => {
    render(<CommandPalette />);
    fireEvent.change(screen.getByPlaceholderText("Type the name of a command"), { target: { value: "zzz" } });
    expect(screen.queryAllByRole("option")).toEqual([]);
    expect(screen.getByText("No matching command")).toBeTruthy();
  });
});
