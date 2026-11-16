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
// The settings' save, faked: Advanced tools are turned on or off from View.
const saved = vi.hoisted(() => ({ settings: [] as unknown[] }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    setAppSettings: async (settings: unknown) => {
      saved.settings.push(settings);
      return null;
    },
  },
}));
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
const { useAppSettings } = await import("@/settings/app-settings.ts");

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
    useAppSettings.setState({ advancedTools: true });
    saved.settings.length = 0;
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

  it("draws Windows' own glyph on each of the window's buttons", () => {
    renderBar();
    const glyph = (name: string) =>
      screen.getByRole("button", { name }).querySelector("[data-icon]")?.getAttribute("data-icon");
    expect([glyph("Minimize"), glyph("Maximize"), glyph("Close")]).toEqual(["minimize", "maximize", "close"]);
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

  it("offers neither the panel's toggle nor View's Panel without Advanced tools", () => {
    useAppSettings.setState({ advancedTools: false });
    renderBar();
    expect(screen.queryByRole("button", { name: "Toggle the panel" })).toBeNull();
    openMenu("View");
    expect(screen.queryByRole("menuitemcheckbox", { name: /^Panel/ })).toBeNull();
    expect(screen.getByRole("menuitemcheckbox", { name: "Advanced tools" }).getAttribute("aria-checked")).toBe("false");
  });

  it("turns Advanced tools on from View, which shows and saves them", async () => {
    useAppSettings.setState({ advancedTools: false });
    useLayout.setState({ visible: { ...shown, panel: false } });
    renderBar();
    openMenu("View");
    fireEvent.click(screen.getByRole("menuitemcheckbox", { name: "Advanced tools" }));
    expect(useAppSettings.getState().advancedTools).toBe(true);
    expect(useLayout.getState().visible.panel).toBe(true);
    await vi.waitFor(() => expect(saved.settings).toEqual([{ advancedTools: true }]));
    expect(screen.getByRole("button", { name: "Toggle the panel" })).toBeTruthy();
  });

  it("turns Advanced tools off from View, which hides the panel", async () => {
    renderBar();
    openMenu("View");
    const advanced = screen.getByRole("menuitemcheckbox", { name: "Advanced tools" });
    expect(advanced.getAttribute("aria-checked")).toBe("true");
    fireEvent.click(advanced);
    expect(useLayout.getState().visible.panel).toBe(false);
    await vi.waitFor(() => expect(saved.settings).toEqual([{ advancedTools: false }]));
  });
});
