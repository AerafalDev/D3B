import { cleanup, render, screen } from "@testing-library/react";
import type { IDockviewHeaderActionsProps } from "dockview-react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { EditorActions } from "./editor-actions.tsx";

/** The props dockview gives the actions of a group whose active tab holds `component`, or no tab. */
function actions(component?: string) {
  const activePanel =
    component === undefined ? undefined : { id: `${component}:a`, view: { contentComponent: component } };
  return render(<EditorActions {...({ activePanel } as unknown as IDockviewHeaderActionsProps)} />);
}

describe("the editor actions", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("shows the actions of the editor the active tab holds", () => {
    actions("blueprint");
    expect(screen.getByRole("group", { name: "Vue du blueprint" })).toBeTruthy();
  });

  it("shows nothing for an editor without actions, or with no tab", () => {
    expect(actions("character").container.innerHTML).toBe("");
    cleanup();
    expect(actions().container.innerHTML).toBe("");
  });
});
