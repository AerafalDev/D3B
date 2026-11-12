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
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: true }, paletteOpen: false });
  });
  afterEach(cleanup);

  it("drives the window on Windows, and offers to restore it once maximised", async () => {
    renderBar();
    fireEvent.click(screen.getByRole("button", { name: "Minimize" }));
    fireEvent.click(screen.getByRole("button", { name: "Maximize" }));
    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(window.minimize).toHaveBeenCalledOnce();
    expect(window.toggleMaximize).toHaveBeenCalledOnce();
    expect(window.close).toHaveBeenCalledOnce();

    window.maximized = true;
    await act(async () => window.resized?.());
    expect(await screen.findByRole("button", { name: "Restore" })).toBeTruthy();
  });

  it("leaves the window's buttons to the system elsewhere", () => {
    window.platform = "linux";
    renderBar();
    expect(screen.queryByRole("button", { name: "Minimize" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Close" })).toBeNull();
  });

  it("toggles each part, pressed while it is shown, and opens the palette from the command centre", () => {
    renderBar();
    const panel = screen.getByRole("button", { name: "Toggle the panel" });
    expect(panel.getAttribute("aria-pressed")).toBe("true");
    fireEvent.click(panel);
    expect(useLayout.getState().visible.panel).toBe(false);
    expect(panel.getAttribute("aria-pressed")).toBe("false");

    fireEvent.click(screen.getByRole("button", { name: "Show all commands (Ctrl+Shift+P)" }));
    expect(useLayout.getState().paletteOpen).toBe(true);
  });
});
