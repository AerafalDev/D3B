import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end, the webview and the notifications, faked: what is read and saved, and what a failure says.
const backEnd = vi.hoisted(() => ({ read: vi.fn(), save: vi.fn(), tauri: true, toasts: [] as string[] }));
vi.mock("@/bindings.ts", () => ({ commands: { appSettings: backEnd.read, setAppSettings: backEnd.save } }));
vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => backEnd.tauri }));
vi.mock("sonner", () => ({ toast: { error: (text: string) => backEnd.toasts.push(text) } }));

const { useAppSettings } = await import("./app-settings.ts");
const { useLayout } = await import("@/shell/layout-store.ts");

describe("the app's settings", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    backEnd.read.mockReset();
    backEnd.save.mockReset();
    backEnd.tauri = true;
    backEnd.toasts.length = 0;
    useAppSettings.setState({ advancedTools: false, changed: false });
    useLayout.setState({ visible: { primarySideBar: true, panel: false, secondarySideBar: false } });
  });

  it("start with Advanced tools off and the panel hidden", async () => {
    vi.resetModules();
    const fresh = await import("./app-settings.ts");
    const layout = await import("@/shell/layout-store.ts");
    expect(fresh.useAppSettings.getState().advancedTools).toBe(false);
    expect(layout.useLayout.getState().visible.panel).toBe(false);
  });

  it("read Advanced tools from the player's folder at start, which shows the panel", async () => {
    backEnd.read.mockResolvedValue({ advancedTools: true });
    await useAppSettings.getState().load();
    expect(useAppSettings.getState().advancedTools).toBe(true);
    expect(useLayout.getState().visible.panel).toBe(true);
  });

  it("keep the panel hidden when the file says off", async () => {
    backEnd.read.mockResolvedValue({ advancedTools: false });
    await useAppSettings.getState().load();
    expect(useLayout.getState().visible.panel).toBe(false);
  });

  it("are the defaults in the browser preview, asking nothing", async () => {
    backEnd.tauri = false;
    await useAppSettings.getState().load();
    useAppSettings.getState().setAdvancedTools(true);
    expect(backEnd.read).not.toHaveBeenCalled();
    expect(backEnd.save).not.toHaveBeenCalled();
    expect(useAppSettings.getState().advancedTools).toBe(true);
  });

  it("save Advanced tools turned on or off, showing or hiding the panel with them", () => {
    backEnd.save.mockResolvedValue(null);
    useAppSettings.getState().setAdvancedTools(true);
    expect(useLayout.getState().visible.panel).toBe(true);
    useAppSettings.getState().setAdvancedTools(false);
    expect(useLayout.getState().visible.panel).toBe(false);
    expect(backEnd.save.mock.calls).toEqual([[{ advancedTools: true }], [{ advancedTools: false }]]);
  });

  it("keep a setting not saved for the session, and say why", async () => {
    backEnd.save.mockRejectedValue({ kind: "notSaved", reason: "disk full" });
    useAppSettings.getState().setAdvancedTools(true);
    await vi.waitFor(() => expect(backEnd.toasts).toEqual(["The setting holds until D3B closes: disk full"]));
    expect(useAppSettings.getState().advancedTools).toBe(true);
  });

  it("say when the system names no folder to keep it in", async () => {
    backEnd.save.mockRejectedValue({ kind: "noFolder" });
    useAppSettings.getState().setAdvancedTools(true);
    await vi.waitFor(() =>
      expect(backEnd.toasts).toEqual(["The setting holds until D3B closes: this system names no folder for D3B"]),
    );
  });

  it("drop a read that ends after the player changed the setting", async () => {
    let answer: (settings: { advancedTools: boolean }) => void = () => {};
    backEnd.read.mockReturnValue(new Promise((resolve) => (answer = resolve)));
    backEnd.save.mockResolvedValue(null);
    const loading = useAppSettings.getState().load();
    useAppSettings.getState().setAdvancedTools(true);
    answer({ advancedTools: false });
    await loading;
    expect(useAppSettings.getState().advancedTools).toBe(true);
    expect(useLayout.getState().visible.panel).toBe(true);
  });

  it("keep the defaults when the read fails", async () => {
    backEnd.read.mockRejectedValue(new Error("no back end"));
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    await useAppSettings.getState().load();
    expect(useAppSettings.getState().advancedTools).toBe(false);
    expect(error).toHaveBeenCalled();
    error.mockRestore();
  });
});
