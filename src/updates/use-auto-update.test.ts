import { renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

type CloseEvent = { preventDefault: () => void };
type CloseHandler = (event: CloseEvent) => Promise<void>;

const window = vi.hoisted(() => ({ handler: null as CloseHandler | null }));
const updater = vi.hoisted(() => ({
  checkAndDownload: vi.fn(async () => {}),
  installOnClose: vi.fn(async () => {}),
  closeAction: vi.fn(() => "close" as "close" | "wait" | "install"),
}));

vi.mock("@tauri-apps/api/window", () => ({
  getCurrentWindow: () => ({
    onCloseRequested: async (handler: CloseHandler) => {
      window.handler = handler;
      return () => {
        window.handler = null;
      };
    },
  }),
}));
vi.mock("./updater.ts", () => ({ useUpdater: { getState: () => updater } }));

const { useAutoUpdate } = await import("./use-auto-update.ts");

async function requestClose(): Promise<boolean> {
  let prevented = false;
  await window.handler?.({ preventDefault: () => (prevented = true) });
  return prevented;
}

describe("useAutoUpdate", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.handler = null;
    Reflect.set(globalThis, "__TAURI_INTERNALS__", {});
    vi.stubEnv("DEV", false);
  });

  afterEach(() => {
    Reflect.deleteProperty(globalThis, "__TAURI_INTERNALS__");
    vi.unstubAllEnvs();
  });

  it("checks for an update once at start", () => {
    renderHook(() => useAutoUpdate());
    expect(updater.checkAndDownload).toHaveBeenCalledOnce();
  });

  it("lets the window close when nothing is to install", async () => {
    renderHook(() => useAutoUpdate());
    await vi.waitFor(() => expect(window.handler).not.toBeNull());

    expect(await requestClose()).toBe(false);
    expect(updater.installOnClose).not.toHaveBeenCalled();
  });

  it("keeps the window open while an install is under way", async () => {
    updater.closeAction.mockReturnValue("wait");
    renderHook(() => useAutoUpdate());
    await vi.waitFor(() => expect(window.handler).not.toBeNull());

    expect(await requestClose()).toBe(true);
    expect(updater.installOnClose).not.toHaveBeenCalled();
  });

  it("installs the downloaded update instead of closing", async () => {
    updater.closeAction.mockReturnValue("install");
    renderHook(() => useAutoUpdate());
    await vi.waitFor(() => expect(window.handler).not.toBeNull());

    expect(await requestClose()).toBe(true);
    expect(updater.installOnClose).toHaveBeenCalledOnce();
  });

  it("does nothing in development", () => {
    vi.stubEnv("DEV", true);
    renderHook(() => useAutoUpdate());
    expect(updater.checkAndDownload).not.toHaveBeenCalled();
  });
});
