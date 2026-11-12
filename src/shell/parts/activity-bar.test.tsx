import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { useLayout } from "../layout-store.ts";
import { ActivityBar } from "./activity-bar.tsx";

function renderBar() {
  return render(
    <TooltipProvider>
      <ActivityBar />
    </TooltipProvider>,
  );
}

describe("the activity bar", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() =>
    useLayout.setState({ view: "characters", visible: { primarySideBar: true, panel: true, secondarySideBar: false } }),
  );
  afterEach(cleanup);

  it("presses the entry of the view the side bar shows", () => {
    renderBar();
    expect(screen.getByRole("button", { name: "Characters" }).getAttribute("aria-pressed")).toBe("true");
  });

  it("hides the side bar when its view's entry is chosen again, and shows it back", () => {
    renderBar();
    const characters = screen.getByRole("button", { name: "Characters" });
    fireEvent.click(characters);
    expect(useLayout.getState().visible.primarySideBar).toBe(false);
    expect(characters.getAttribute("aria-pressed")).toBe("false");
    fireEvent.click(characters);
    expect(useLayout.getState().visible.primarySideBar).toBe(true);
  });
});
