import { beforeEach, describe, expect, it } from "vitest";

import { useLayout } from "./layout-store.ts";

describe("showView", () => {
  beforeEach(() =>
    useLayout.setState({
      view: "characters",
      visible: { primarySideBar: false, panel: true, secondarySideBar: false },
    }),
  );

  it("shows the side bar with the view chosen", () => {
    useLayout.getState().showView("characters");
    expect(useLayout.getState().visible.primarySideBar).toBe(true);
    expect(useLayout.getState().view).toBe("characters");
  });

  it("hides the side bar when the view chosen is already showing, as VS Code's activity bar does", () => {
    useLayout.getState().showView("characters");
    useLayout.getState().showView("characters");
    expect(useLayout.getState().visible.primarySideBar).toBe(false);
  });
});
