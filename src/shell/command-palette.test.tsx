import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

vi.mock("@/lib/platform.ts", () => ({ platform: () => "windows" }));

const { CommandPalette } = await import("./command-palette.tsx");
const { useLayout } = await import("./layout-store.ts");

describe("the command palette", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() =>
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: true }, paletteOpen: true }),
  );
  afterEach(cleanup);

  it("lists the commands with their shortcuts", () => {
    render(<CommandPalette />);
    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Toggle the primary side barCtrl+B",
      "Toggle the panelCtrl+J",
      "Toggle the secondary side barCtrl+Alt+B",
    ]);
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
