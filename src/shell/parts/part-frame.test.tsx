import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { useLayout } from "../layout-store.ts";
import { PartFrame } from "./part-frame.tsx";

describe("a part's frame", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("hides its own part only, and says its views come with the features", () => {
    useLayout.setState({ visible: { primarySideBar: true, panel: true, secondarySideBar: true } });
    render(
      <TooltipProvider>
        <PartFrame part="panel" />
      </TooltipProvider>,
    );
    expect(screen.getByRole("region", { name: "Panneau" }).textContent).toContain("arrivent avec les fonctionnalités");

    fireEvent.click(screen.getByRole("button", { name: "Masquer le panneau" }));
    expect(useLayout.getState().visible).toEqual({ primarySideBar: true, panel: false, secondarySideBar: true });
  });

  it("names its view in its header and shows it in place of the empty line", () => {
    render(
      <TooltipProvider>
        <PartFrame part="primarySideBar" title="Personnages">
          <p>la liste</p>
        </PartFrame>
      </TooltipProvider>,
    );
    expect(screen.getByRole("heading", { name: "Personnages" })).toBeTruthy();
    expect(screen.getByText("la liste")).toBeTruthy();
    expect(screen.queryByText(/arrivent avec les fonctionnalités/)).toBeNull();
  });
});
