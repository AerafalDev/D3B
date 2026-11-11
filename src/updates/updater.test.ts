import { beforeEach, describe, expect, it, vi } from "vitest";

const calls: string[] = [];
const plugin = vi.hoisted(() => ({
  check: vi.fn(),
  relaunch: vi.fn(),
  exit: vi.fn(),
  logError: vi.fn(async () => {}),
  relayRestore: vi.fn(async () => void calls.push("restore")),
}));

vi.mock("@tauri-apps/plugin-updater", () => ({ check: plugin.check }));
vi.mock("@tauri-apps/plugin-process", () => ({ relaunch: plugin.relaunch, exit: plugin.exit }));
vi.mock("@tauri-apps/plugin-log", () => ({ error: plugin.logError }));
vi.mock("@/bindings.ts", () => ({ commands: { relayRestore: plugin.relayRestore } }));

const { useUpdater } = await import("./updater.ts");

function fakeUpdate(version: string, install = vi.fn(async (_options?: unknown) => void calls.push("install"))) {
  return { version, body: "Notes", download: vi.fn(async () => {}), install };
}

async function downloadVersion(version: string, update = fakeUpdate(version)) {
  plugin.check.mockResolvedValue(update);
  await useUpdater.getState().checkAndDownload();
  return update;
}

describe("useUpdater", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    calls.length = 0;
    plugin.relaunch.mockImplementation(async () => void calls.push("relaunch"));
    plugin.exit.mockImplementation(async () => void calls.push("exit"));
    plugin.relayRestore.mockImplementation(async () => void calls.push("restore"));
    useUpdater.setState({ state: { status: "idle" } });
    plugin.check.mockResolvedValue(null);
    await useUpdater.getState().checkAndDownload();
  });

  it("downloads a newer version and offers to restart into it", async () => {
    const update = await downloadVersion("0.11.0");

    expect(update.download).toHaveBeenCalledOnce();
    expect(useUpdater.getState().state).toEqual({ status: "ready", version: "0.11.0", notes: "Notes" });
  });

  it("stays idle when the app is up to date", () => {
    expect(useUpdater.getState().state).toEqual({ status: "idle" });
    expect(useUpdater.getState().closeAction()).toBe("close");
  });

  it("reports a failed check and logs it", async () => {
    plugin.check.mockRejectedValue(new Error("offline"));

    await useUpdater.getState().checkAndDownload();

    expect(useUpdater.getState().state).toEqual({ status: "failed", error: "offline" });
    expect(plugin.logError).toHaveBeenCalledWith("update: check or download: offline");
  });

  it("reports a failed download and keeps nothing to install", async () => {
    const update = fakeUpdate("0.11.0");
    update.download.mockRejectedValue(new Error("network"));

    await downloadVersion("0.11.0", update);

    expect(useUpdater.getState().state).toEqual({ status: "failed", error: "network" });
    expect(useUpdater.getState().closeAction()).toBe("close");
  });

  it("checks only once while a check is under way", async () => {
    plugin.check.mockReturnValue(new Promise(() => {}));
    void useUpdater.getState().checkAndDownload();

    await useUpdater.getState().checkAndDownload();

    expect(plugin.check).toHaveBeenCalledTimes(2);
  });

  it("installs, then relaunches, on restart", async () => {
    await downloadVersion("0.11.0");

    await useUpdater.getState().restart();

    expect(calls).toEqual(["restore", "install", "relaunch"]);
  });

  it("installs even when the client cannot be put back first, and logs it", async () => {
    plugin.relayRestore.mockRejectedValue(new Error("version locked"));
    await downloadVersion("0.11.0");

    await useUpdater.getState().restart();

    expect(calls).toEqual(["install", "relaunch"]);
    expect(plugin.logError).toHaveBeenCalledWith("update: restore the client before installing: version locked");
  });

  it("offers the update again, and installs it on close, when the restart's install fails", async () => {
    const install = vi.fn(async () => {
      throw new Error("locked");
    });
    await downloadVersion("0.11.0", fakeUpdate("0.11.0", install));

    await useUpdater.getState().restart();

    expect(useUpdater.getState().state.status).toBe("ready");
    expect(useUpdater.getState().closeAction()).toBe("install");
    expect(plugin.relaunch).not.toHaveBeenCalled();
  });

  it("waits for an install under way before closing", async () => {
    await downloadVersion("0.11.0");
    useUpdater.setState({ state: { status: "installing", version: "0.11.0" } });

    expect(useUpdater.getState().closeAction()).toBe("wait");
  });

  it("installs without restarting, then quits, on close", async () => {
    const update = await downloadVersion("0.11.0");

    expect(useUpdater.getState().closeAction()).toBe("install");
    await useUpdater.getState().installOnClose();

    expect(update.install).toHaveBeenCalledWith({ restartAfterInstall: false });
    expect(calls).toEqual(["restore", "install", "exit"]);
  });
});
