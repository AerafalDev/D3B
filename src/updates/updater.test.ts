import { beforeEach, describe, expect, it, vi } from "vitest";

const plugin = vi.hoisted(() => ({
  check: vi.fn(),
  relaunch: vi.fn(),
  exit: vi.fn(),
}));

vi.mock("@tauri-apps/plugin-updater", () => ({ check: plugin.check }));
vi.mock("@tauri-apps/plugin-process", () => ({ relaunch: plugin.relaunch, exit: plugin.exit }));

const { useUpdater } = await import("./updater.ts");

function fakeUpdate(version: string) {
  return { version, body: "Notes", download: vi.fn(async () => {}), install: vi.fn(async () => {}) };
}

describe("useUpdater", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useUpdater.setState({ state: { status: "idle" } });
  });

  it("downloads a newer version and offers to restart into it", async () => {
    const update = fakeUpdate("0.11.0");
    plugin.check.mockResolvedValue(update);

    await useUpdater.getState().checkAndDownload();

    expect(update.download).toHaveBeenCalledOnce();
    expect(useUpdater.getState().state).toEqual({ status: "ready", version: "0.11.0", notes: "Notes" });
  });

  it("stays idle when the app is up to date", async () => {
    plugin.check.mockResolvedValue(null);

    await useUpdater.getState().checkAndDownload();

    expect(useUpdater.getState().state).toEqual({ status: "idle" });
  });

  it("reports a failed check without throwing", async () => {
    plugin.check.mockRejectedValue(new Error("offline"));

    await useUpdater.getState().checkAndDownload();

    expect(useUpdater.getState().state).toEqual({ status: "failed", error: "offline" });
  });

  it("installs and relaunches on restart", async () => {
    const update = fakeUpdate("0.11.0");
    plugin.check.mockResolvedValue(update);
    await useUpdater.getState().checkAndDownload();

    await useUpdater.getState().restart();

    expect(update.install).toHaveBeenCalledOnce();
    expect(plugin.relaunch).toHaveBeenCalledOnce();
  });

  it("installs on close only when an update was downloaded", async () => {
    plugin.check.mockResolvedValue(null);
    await useUpdater.getState().checkAndDownload();
    expect(await useUpdater.getState().installOnClose()).toBe(false);

    const update = fakeUpdate("0.11.0");
    plugin.check.mockResolvedValue(update);
    await useUpdater.getState().checkAndDownload();
    expect(await useUpdater.getState().installOnClose()).toBe(true);
    expect(update.install).toHaveBeenCalledOnce();
    expect(plugin.exit).toHaveBeenCalledWith(0);
  });
});
