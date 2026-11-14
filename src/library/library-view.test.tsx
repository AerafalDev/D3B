import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { BlueprintSummary } from "./library-store.ts";

// The editor area, faked: the editors opened.
const opened = vi.hoisted(() => ({ editors: [] as unknown[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: unknown) => opened.editors.push(editor),
  useEditors: (select: (store: { active: string | null }) => unknown) => select({ active: "blueprint:hunt" }),
}));

const { useLibrary } = await import("./library-store.ts");
const { LibraryView } = await import("./library-view.tsx");

function blueprint(id: string, given: boolean, more: Partial<BlueprintSummary> = {}): BlueprintSummary {
  const base = { id, name: id, description: `${id}'s description`, given, version: 1, basedOn: null, problems: 0 };
  return { ...base, variables: [], ...more };
}

describe("the Library view", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    opened.editors = [];
    useLibrary.setState({
      blueprints: [
        blueprint("automatic-harvest", true),
        blueprint("hunt", true),
        blueprint("ma-chasse", false, { name: "Ma chasse", basedOn: { id: "hunt", version: 1 }, problems: 2 }),
      ],
      kept: [{ path: "C:\\Users\\me\\Documents\\D3B\\blueprints\\vieille.json", why: { kind: "newer", found: 2 } }],
    });
  });
  afterEach(cleanup);

  it("lists D3B's blueprints by their names in the player's language, then mine", () => {
    render(<LibraryView />);
    const [given, mine] = screen.getAllByRole("list");
    expect(
      within(given as HTMLElement)
        .getAllByRole("button")
        .map((row) => row.textContent),
    ).toEqual(["Récolte automatique", "Chasse"]);
    expect(within(mine as HTMLElement).getByRole("button").textContent).toBe("Ma chasse22 problèmes"); // the count shown, then said to screen readers
  });

  it("says a blueprint's description, and what a copy is based on, in its hover", () => {
    render(<LibraryView />);
    expect(screen.getByRole("button", { name: /^Chasse/ }).getAttribute("title")).toBe(
      "Combat les monstres d'une zone, vide ses pods à la banque, et recommence.",
    );
    expect(screen.getByRole("button", { name: /^Ma chasse/ }).getAttribute("title")).toBe(
      "ma-chasse's description\nBasé sur Chasse v1",
    );
  });

  it("marks the blueprint whose tab is active, and opens one in its tab", () => {
    render(<LibraryView />);
    expect(screen.getByRole("button", { name: /^Chasse/ }).getAttribute("aria-current")).toBe("true");
    fireEvent.click(screen.getByRole("button", { name: /^Récolte automatique/ }));
    expect(opened.editors).toEqual([
      {
        id: "blueprint:automatic-harvest",
        component: "blueprint",
        title: "Récolte automatique",
        params: { icon: "blueprint", id: "automatic-harvest" },
      },
    ]);
  });

  it("folds the files kept, each saying why in its hover", () => {
    render(<LibraryView />);
    const header = screen.getByRole("button", { name: /Ignorés/ });
    expect(header.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(header);
    const file = screen.getByText("vieille.json");
    expect(file.closest("[title]")?.getAttribute("title")).toBe("Écrit par un D3B plus récent (schéma 2).");
  });

  it("shows no Ignored group when every file reads", () => {
    useLibrary.setState({ kept: [] });
    render(<LibraryView />);
    expect(screen.queryByRole("button", { name: /Ignorés/ })).toBeNull();
  });
});
