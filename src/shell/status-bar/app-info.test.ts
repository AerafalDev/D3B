import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AppInfo_Serialize } from "@/bindings.ts";

const backEnd = vi.hoisted(() => ({
  appInfo: vi.fn<() => Promise<AppInfo_Serialize>>(),
  tauri: true,
}));

vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => backEnd.tauri }));
vi.mock("@/bindings.ts", () => ({ commands: { appInfo: backEnd.appInfo } }));

const { useAppInfo } = await import("./app-info.ts");

const info: AppInfo_Serialize = { version: "0.13.0", gameBuild: "3.7.13.17", dataError: null, os: "windows" };

describe("the app's info", () => {
  beforeEach(() => {
    useAppInfo.setState({ info: null });
    backEnd.appInfo.mockReset();
    backEnd.tauri = true;
  });

  it("asks the back end once and keeps its answer", async () => {
    backEnd.appInfo.mockResolvedValue(info);
    await useAppInfo.getState().load();
    await useAppInfo.getState().load();
    expect(useAppInfo.getState().info).toEqual(info);
    expect(backEnd.appInfo).toHaveBeenCalledTimes(1);
  });

  it("asks nothing in the browser preview", async () => {
    backEnd.tauri = false;
    await useAppInfo.getState().load();
    expect(backEnd.appInfo).not.toHaveBeenCalled();
    expect(useAppInfo.getState().info).toBeNull();
  });

  it("stays unknown, and asks again later, when the back end fails", async () => {
    backEnd.appInfo.mockRejectedValueOnce(new Error("no back end")).mockResolvedValueOnce(info);
    await useAppInfo.getState().load();
    expect(useAppInfo.getState().info).toBeNull();
    await useAppInfo.getState().load();
    expect(useAppInfo.getState().info).toEqual(info);
  });
});
