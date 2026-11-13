import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import type { AppInfo_Serialize } from "@/bindings.ts";
import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { icons } from "@/icons/icons.ts";
import { useAppInfo } from "./app-info.ts";
import { StatusBar } from "./status-bar.tsx";

const info: AppInfo_Serialize = { version: "0.13.0", gameBuild: "3.7.13.17", dataError: null, os: "windows" };

function renderBar() {
  return render(
    <TooltipProvider>
      <StatusBar />
    </TooltipProvider>,
  );
}

describe("the status bar", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => useAppInfo.setState({ info: null }));
  afterEach(cleanup);

  it("names the app alone until the back end tells its version, as in the browser preview", () => {
    renderBar();
    expect(screen.getByText("D3B")).toBeTruthy();
    expect(screen.queryByText(/Client/)).toBeNull();
    expect(screen.queryByText("No game data")).toBeNull();
  });

  it("names the app's version and the client build of its game data", () => {
    useAppInfo.setState({ info });
    renderBar();
    expect(screen.getByText("D3B 0.13.0")).toBeTruthy();
    expect(screen.getByText("Client 3.7.13.17")).toBeTruthy();
  });

  it("says there is no game data, and why in its hover", async () => {
    useAppInfo.setState({ info: { ...info, gameBuild: null, dataError: "data format 2 is not supported" } });
    const { container } = renderBar();
    const shown = [...container.querySelectorAll("img")].map((image) => image.getAttribute("src"));
    expect(shown).toEqual([icons.warning]);
    const item = screen.getByText("No game data").closest("[tabindex]");
    if (item === null) {
      throw new Error("the item takes no focus");
    }
    fireEvent.focus(item);
    expect((await screen.findAllByText("data format 2 is not supported")).length).toBeGreaterThan(0);
  });

  it("opens no hover over the data's build, which would only repeat it (the owner, 2026-11-13)", () => {
    useAppInfo.setState({ info });
    renderBar();
    expect(screen.getByText("Client 3.7.13.17").closest("[tabindex]")).toBeNull();
  });
});
