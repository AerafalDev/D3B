import { cleanup, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { BlueprintTab } from "./blueprint-tab.tsx";
import { type BlueprintSummary, useLibrary } from "./library-store.ts";
import { valueText } from "./value-text.ts";

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
    const settings = [...container.querySelectorAll("dt")].map(
      (term) => `${term.textContent}: ${term.nextSibling?.textContent}`,
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

  it("says the blueprint is gone once its file is", () => {
    useLibrary.setState({ blueprints: [] });
    tab("automatic-harvest");
    expect(screen.getByText("Ce blueprint n'est plus là.")).toBeTruthy();
  });
});

describe("valueText", () => {
  it("reads each type's shapes, and what it does not hold as its JSON", () => {
    const read = (type: string, json: string | null) => valueText(type, json, "fr").replace(/[  ]/g, " ");
    expect(read("percentage", "90")).toBe("90 %");
    expect(read("number", "5000")).toBe("5 000");
    expect(read("bool", "true")).toBe("Oui");
    expect(read("bank", '{"map": 191104002}')).toBe("Celle de la carte 191104002");
    expect(read("items", '"all"')).toBe("Tout");
    expect(read("items", "[289, 400]")).toBe("2 objets");
    expect(read("text", '"Objectif"')).toBe("Objectif");
    expect(read("percentage", '"ninety"')).toBe('"ninety"');
    expect(read("text", "{")).toBe("{");
  });
});
