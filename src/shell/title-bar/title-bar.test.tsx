import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";

const window = vi.hoisted(() => ({
  platform: "windows" as "windows" | "mac" | "linux",
  maximized: false,
  resized: null as (() => void) | null,
  minimize: vi.fn(async () => {}),
  toggleMaximize: vi.fn(async () => {}),
  close: vi.fn(async () => {}),
}));

vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@/lib/platform.ts", () => ({ platform: () => window.platform }));
vi.mock("@tauri-apps/api/window", () => ({
  getCurrentWindow: () => ({
    isMaximized: async () => window.maximized,
    onResized: async (listener: () => void) => {
      window.resized = listener;
      return () => {};
    },
    minimize: window.minimize,
    toggleMaximize: window.toggleMaximize,
    close: window.close,
  }),
}));

const { TitleBar } = await import("./title-bar.tsx");
const { useLayout } = await import("../layout-store.ts");

const shown = { primarySideBar: true, panel: true, secondarySideBar: true };

/** Opens a menu as a press of the mouse does: Radix opens a menu bar's menu on pointer down. */
function openMenu(name: string) {
  fireEvent.pointerDown(screen.getByRole("menuitem", { name }), { button: 0, ctrlKey: false, pointerType: "mouse" });
}

function renderBar() {
  return render(
    <TooltipProvider>
      <TitleBar />
    </TooltipProvider>,
  );
}

describe("the title bar", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    window.platform = "windows";
    window.maximized = false;
    vi.clearAllMocks();
    useLayout.setState({ visible: { ...shown }, paletteOpen: false });
  });
  afterEach(cleanup);

  it("minimises, maximises and closes the window on Windows", () => {
    renderBar();
    fireEvent.click(screen.getByRole("button", { name: "Minimize" }));
    fireEvent.click(screen.getByRole("button", { name: "Maximize" }));
    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(window.minimize).toHaveBeenCalledOnce();
    expect(window.toggleMaximize).toHaveBeenCalledOnce();
    expect(window.close).toHaveBeenCalledOnce();
  });

  it("offers to restore the window once it is maximised, with Windows' restore glyph", async () => {
    renderBar();
    expect(
      screen.getByRole("button", { name: "Maximize" }).querySelector("[data-icon]")?.getAttribute("data-icon"),
    ).toBe("maximize");
    window.maximized = true;
    await act(async () => window.resized?.());
    const restore = await screen.findByRole("button", { name: "Restore" });
    expect(restore.querySelector("[data-icon]")?.getAttribute("data-icon")).toBe("restore");
  });

  it.each(["linux", "mac"] as const)("leaves the window's buttons to the system on %s", (system) => {
    window.platform = system;
    renderBar();
    expect(screen.queryByRole("button", { name: "Minimize" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Close" })).toBeNull();
  });

  it("draws no menus on macOS, whose menu bar holds them", () => {
    window.platform = "mac";
    renderBar();
    expect(screen.queryByRole("menuitem", { name: "File" })).toBeNull();
  });

  it.each([
    ["Toggle the primary side bar", "primarySideBar"],
    ["Toggle the panel", "panel"],
    ["Toggle the secondary side bar", "secondarySideBar"],
  ] as const)("%s from its toggle, pressed while its part shows", (name, part) => {
    renderBar();
    const toggle = screen.getByRole("button", { name });
    expect(toggle.getAttribute("aria-pressed")).toBe("true");
    fireEvent.click(toggle);
    expect(useLayout.getState().visible).toEqual({ ...shown, [part]: false });
    expect(toggle.getAttribute("aria-pressed")).toBe("false");
  });

  it("opens the palette from the command centre", () => {
    renderBar();
    fireEvent.click(screen.getByRole("button", { name: "Show all commands (Ctrl+Shift+P)" }));
    expect(useLayout.getState().paletteOpen).toBe(true);
  });

  it("closes the window from File, Exit, through its close request so an update still installs", () => {
    renderBar();
    openMenu("File");
    fireEvent.click(screen.getByRole("menuitem", { name: "Exit" }));
    expect(window.close).toHaveBeenCalledOnce();
  });

  it("checks in View each part shown, and hides that part only when chosen", () => {
    renderBar();
    openMenu("View");
    const panel = screen.getByRole("menuitemcheckbox", { name: /^Panel/ });
    expect(panel.getAttribute("aria-checked")).toBe("true");
    fireEvent.click(panel);
    expect(useLayout.getState().visible).toEqual({ ...shown, panel: false });
  });
});
