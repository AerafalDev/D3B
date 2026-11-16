import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { BlueprintSummary } from "./library-store.ts";

// The editor area, faked: the editors opened.
const opened = vi.hoisted(() => ({ editors: [] as unknown[], closed: [] as string[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: unknown) => opened.editors.push(editor),
  closeEditor: (id: string) => opened.closed.push(id),
  useEditors: (select: (store: { active: string | null }) => unknown) => select({ active: "blueprint:hunt" }),
}));

const { useLibrary } = await import("./library-store.ts");
const { LibraryView } = await import("./library-view.tsx");
const { useGraphs } = await import("@/blueprint/graph-store.ts");

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
    opened.closed = [];
    useLibrary.setState({
      blueprints: [
        blueprint("automatic-harvest", true),
        blueprint("hunt", true),
        blueprint("ma-chasse", false, {
          name: "Ma chasse",
          basedOn: { id: "hunt", version: 1, newer: null },
          problems: 2,
        }),
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

  it("marks a copy whose original has a newer version, and says it in its hover", () => {
    useLibrary.setState({
      blueprints: [
        blueprint("hunt", true, { version: 2 }),
        blueprint("ma-chasse", false, { name: "Ma chasse", basedOn: { id: "hunt", version: 1, newer: 2 } }),
      ],
    });
    render(<LibraryView />);
    const row = screen.getByRole("button", { name: /^Ma chasse/ });
    expect(row.textContent).toBe("Ma chasseSon original a une version plus récente, v2");
    expect(row.getAttribute("title")).toBe(
      "ma-chasse's description\nBasé sur Chasse v1\nSon original a une version plus récente, v2",
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

  it("folds the files kept, each saying why in its hover, and shows none when every file reads", () => {
    render(<LibraryView />);
    const header = screen.getByRole("button", { name: /Ignorés/ });
    expect(header.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(header);
    const file = screen.getByText("vieille.json");
    expect(file.closest("[title]")?.getAttribute("title")).toBe("Écrit par un D3B plus récent (schéma 2).");
    cleanup();
    useLibrary.setState({ kept: [] });
    render(<LibraryView />);
    expect(screen.queryByRole("button", { name: /Ignorés/ })).toBeNull();
  });

  it("offers Delete on mine only, which closes its tab and drops its graphs once it went to the trash", async () => {
    const remove = vi.fn().mockResolvedValue(true);
    useLibrary.setState({ remove });
    useGraphs.setState({ read: { "ma-chasse": { state: "loading" } } });
    render(<LibraryView />);
    fireEvent.contextMenu(screen.getByRole("button", { name: /^Chasse/ }));
    expect(await screen.findByRole("menuitem", { name: "Dupliquer" })).toBeTruthy();
    expect(screen.queryByRole("menuitem", { name: "Supprimer" })).toBeNull();
    fireEvent.keyDown(document.activeElement ?? document.body, { key: "Escape" });
    fireEvent.contextMenu(screen.getByRole("button", { name: /^Ma chasse/ }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Supprimer" }));
    await vi.waitFor(() => expect(opened.closed).toEqual(["blueprint:ma-chasse"]));
    expect(remove).toHaveBeenCalledWith(expect.objectContaining({ id: "ma-chasse" }));
    expect(useGraphs.getState().read["ma-chasse"]).toBeUndefined();
  });

  it("keeps the tab and its graphs when Delete failed", async () => {
    const remove = vi.fn().mockResolvedValue(false);
    useLibrary.setState({ remove });
    useGraphs.setState({ read: { "ma-chasse": { state: "loading" } } });
    render(<LibraryView />);
    fireEvent.contextMenu(screen.getByRole("button", { name: /^Ma chasse/ }));
    fireEvent.click(await screen.findByRole("menuitem", { name: "Supprimer" }));
    await vi.waitFor(() => expect(remove).toHaveBeenCalled());
    await Promise.resolve();
    expect(opened.closed).toEqual([]);
    expect(useGraphs.getState().read["ma-chasse"]).toEqual({ state: "loading" });
  });
});
