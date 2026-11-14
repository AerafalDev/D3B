import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { type BlueprintSummary, useLibrary } from "./library-store.ts";
import { valueText } from "./value-text.ts";

// The editor area, faked: the editors opened.
const opened = vi.hoisted(() => ({ editors: [] as unknown[] }));
vi.mock("@/shell/parts/editor-store.ts", () => ({ openEditor: (editor: unknown) => opened.editors.push(editor) }));

const { BlueprintTab } = await import("./blueprint-tab.tsx");

const variable = (id: string, valueType: string, json: string | null, perCharacter = true) => ({
  id,
  name: id,
  valueType,
  default: json,
  perCharacter,
  memory: false,
  help: "",
});

const harvest: BlueprintSummary = {
  id: "automatic-harvest",
  name: "Automatic harvest",
  description: "Harvests what grows.",
  given: true,
  version: 1,
  basedOn: null,
  problems: 0,
  variables: [
    variable("zone", "zone", null),
    variable("pods", "percentage", "90"),
    variable("bank", "bank", '"nearest"'),
    variable("keep", "items", '"none"'),
    variable("harvests", "number", "0", false),
  ],
};

const titles: string[] = [];

function tab(id: string) {
  // The tab reads its params and sets its title; the rest of the panel's API plays no part.
  const api = { setTitle: (title: string) => titles.push(title) };
  const props = { api, params: { id, icon: "blueprint" } } as unknown as Parameters<typeof BlueprintTab>[0];
  return render(<BlueprintTab {...props} />);
}

describe("a blueprint's tab, before the editor", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("says its name, whose it is, its description and its settings per character with their defaults", () => {
    useLibrary.setState({ blueprints: [harvest] });
    const { container } = tab("automatic-harvest");
    expect(screen.getByRole("heading", { level: 2 }).textContent).toBe("Récolte automatique");
    expect(container.textContent).toContain("Donné par D3B · v1 · verrouillé");
    expect(container.textContent).toContain("Récolte ce qui pousse dans une zone");
    // A setting's name, then its value; its help is said to screen readers inside its name.
    const settings = [...container.querySelectorAll("dt")].map(
      (term) => `${term.firstChild?.textContent}: ${term.nextSibling?.textContent}`,
    );
    expect(settings.map((line) => line.replace(/[  ]/g, " "))).toEqual([
      "Zone: à choisir",
      "Banque à: 90 %",
      "Banque: La plus proche",
      "Garder: Rien",
    ]);
    expect(screen.getByRole("button", { name: "Dupliquer" })).toBeTruthy();
    expect(titles.at(-1)).toBe("Récolte automatique");
  });

  it("says what a copy of mine is based on, and its problems", () => {
    const mine = {
      ...harvest,
      id: "mine",
      name: "Ma récolte",
      given: false,
      basedOn: { id: "hunt", version: 1 },
      problems: 1,
    };
    useLibrary.setState({ blueprints: [mine] });
    const { container } = tab("mine");
    expect(container.textContent).toContain("À moi · basé sur Chasse v1");
    expect(container.textContent).toContain("1 problème");
  });

  it("opens the copy Duplicate made, and makes one copy for two clicks", async () => {
    let made: (id: string | null) => void = () => {};
    const duplicate = vi.fn(() => new Promise<string | null>((resolve) => (made = resolve)));
    useLibrary.setState({ blueprints: [harvest], duplicate });
    opened.editors = [];
    tab("automatic-harvest");
    const button = screen.getByRole("button", { name: "Dupliquer" });
    fireEvent.click(button);
    fireEvent.click(button);
    expect(duplicate).toHaveBeenCalledTimes(1);
    await act(async () => made("recolte-automatique-copie"));
    expect(opened.editors).toEqual([
      {
        id: "blueprint:recolte-automatique-copie",
        component: "blueprint",
        title: "Récolte automatique (copie)",
        params: { icon: "blueprint", id: "recolte-automatique-copie" },
      },
    ]);
  });

  it("opens nothing when Duplicate made no copy", async () => {
    useLibrary.setState({ blueprints: [harvest], duplicate: async () => null });
    opened.editors = [];
    tab("automatic-harvest");
    await act(async () => fireEvent.click(screen.getByRole("button", { name: "Dupliquer" })));
    expect(opened.editors).toEqual([]);
  });

  it("says the blueprint is gone once its file is", () => {
    useLibrary.setState({ blueprints: [] });
    tab("automatic-harvest");
    expect(screen.getByText("Ce blueprint n'est plus là.")).toBeTruthy();
  });
});

describe("valueText", () => {
  const read = (type: string, json: string | null) =>
    valueText(type, json, i18next.language).replace(/[\u00a0\u202f]/g, " ");

  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  it("reads each type's shapes in the player's language", async () => {
    expect(read("percentage", "90")).toBe("90 %");
    expect(read("number", "5000")).toBe("5 000");
    expect(read("bool", "true")).toBe("Oui");
    expect(read("bank", '{"map": 191104002}')).toBe("Celle de la carte 191104002");
    expect(read("items", '"all"')).toBe("Tout");
    expect(read("items", "[289, 400]")).toBe("2 objets");
    expect(read("zone", null)).toBe("à choisir");
    await i18next.changeLanguage("en");
    expect(read("percentage", "90")).toBe("90%");
    await i18next.changeLanguage("fr");
  });

  it("reads a value its type does not hold as its JSON", () => {
    expect(read("percentage", '"ninety"')).toBe('"ninety"');
    expect(read("bool", '"yes"')).toBe('"yes"');
    expect(read("bank", '"farthest"')).toBe('"farthest"');
    expect(read("items", '"some"')).toBe('"some"');
    expect(read("text", "{")).toBe("{");
  });
});
