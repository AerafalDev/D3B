import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { MyBlueprint } from "./my-blueprint.tsx";

const variable = (id: string, valueType: string, json: string | null, perCharacter: boolean) => ({
  id,
  name: id,
  valueType,
  default: json,
  perCharacter,
  memory: false,
  bounds: null,
  help: "",
});

const harvest: BlueprintSummary = {
  id: "automatic-harvest",
  name: "Automatic harvest",
  description: "",
  given: true,
  version: 1,
  basedOn: null,
  problems: 0,
  variables: [variable("pods", "percentage", "90", true), variable("harvests", "number", "0", false)],
};

const graphs = [
  { id: "events", name: "Event graph", function: false, inputs: [], outputs: [], nodes: [], wires: [] },
  { id: "empty_at_bank", name: "Vider à la banque", function: true, inputs: [], outputs: [], nodes: [], wires: [] },
];

describe("My Blueprint", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("lists the event graph, the functions and the variables, marking the graph shown", () => {
    render(<MyBlueprint blueprint={harvest} graphs={graphs} shown="events" onShow={() => {}} />);
    const [events, bank] = screen.getAllByRole("button");
    expect([events?.textContent, bank?.textContent]).toEqual(["Graphe d'événements", "Vider à la banque"]);
    expect(events?.getAttribute("aria-current")).toBe("true");
    expect(bank?.hasAttribute("aria-current")).toBe(false);
    const [graphsList, functionsList] = screen.getAllByRole("list");
    expect([graphsList?.textContent, functionsList?.textContent]).toEqual(["Graphe d'événements", "Vider à la banque"]);
    expect(screen.getByText("Banque à")).toBeTruthy();
  });

  it("says which variables each character sets, with their help and default", () => {
    const { container } = render(<MyBlueprint blueprint={harvest} graphs={graphs} shown="events" onShow={() => {}} />);
    const [pods, harvests] = [...container.querySelectorAll("li[title]")];
    expect(pods?.getAttribute("title")?.replace(/[\u00a0\u202f]/g, " ")).toBe(
      "La part des pods à laquelle le personnage va à la banque.\nPar défaut : 90 %",
    );
    expect(pods?.textContent).toContain("Réglée par personnage");
    expect(harvests?.textContent).not.toContain("Réglée par personnage");
    // The eye, on the variable set per character alone.
    expect([pods, harvests].map((row) => row?.querySelector("svg") !== null)).toEqual([true, false]);
  });

  it("shows the graph chosen", () => {
    const onShow = vi.fn();
    render(<MyBlueprint blueprint={harvest} graphs={graphs} shown="events" onShow={onShow} />);
    fireEvent.click(screen.getByRole("button", { name: "Vider à la banque" }));
    expect(onShow).toHaveBeenCalledWith("empty_at_bank");
  });

  it("names several event graphs by their names, to tell them apart", () => {
    const two = [
      ...graphs,
      { id: "second", name: "Combats", function: false, inputs: [], outputs: [], nodes: [], wires: [] },
    ];
    render(<MyBlueprint blueprint={harvest} graphs={two} shown="events" onShow={() => {}} />);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual([
      "Event graph",
      "Combats",
      "Vider à la banque",
    ]);
  });
});
