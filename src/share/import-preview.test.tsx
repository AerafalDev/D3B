import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { ImportPreview } from "./import-preview.tsx";
import { type SharedPreview, useShare } from "./share-store.ts";

const variable = {
  id: "zone",
  name: "Zone",
  valueType: "zone",
  default: null,
  bounds: null,
  perCharacter: true,
  memory: false,
  help: "Où récolter.",
};

const preview: SharedPreview = {
  path: "Ma récolte.d3b",
  blueprint: {
    id: "ma-recolte",
    name: "Ma récolte",
    description: "Récolte une zone.",
    given: false,
    version: 1,
    basedOn: { id: "automatic-harvest", version: 1, newer: null },
    problems: 1,
    variables: [
      variable,
      { ...variable, id: "harvests", name: "Récoltes", valueType: "number", default: "0", perCharacter: false },
    ],
  },
  problems: [{ kind: "noSuchVariable", graph: "events", node: "n2", pin: null, subject: "pods" }],
  graphs: 2,
  nodes: 9,
  zones: 0,
  routes: 0,
};

describe("the import's preview", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => useShare.setState({ preview: null, importing: false }));
  afterEach(cleanup);

  it("shows nothing until a file is previewed", () => {
    const { container } = render(<ImportPreview />);
    expect(container.innerHTML).toBe("");
  });

  it("shows the blueprint, its settings, its variables, its problems and what it holds", () => {
    useShare.setState({ preview });
    render(<ImportPreview />);
    const dialog = screen.getByRole("dialog");
    expect(screen.getByRole("heading", { name: "Ma récolte" })).toBeTruthy();
    for (const text of [
      "Récolte une zone.",
      "Basé sur Récolte automatique v1",
      "Réglages par personnage",
      "Autres variables",
      "Récoltes",
      "1 problème",
      "Contient : 2 graphes · 9 nœuds · 0 zone · 0 route",
    ]) {
      expect(dialog.textContent).toContain(text);
    }
  });

  it("closes on Cancel without importing", () => {
    useShare.setState({ preview });
    render(<ImportPreview />);
    fireEvent.click(screen.getByRole("button", { name: "Annuler" }));
    expect(useShare.getState().preview).toBeNull();
  });

  it("waits on Import while the blueprint is saved", () => {
    useShare.setState({ preview, importing: true });
    render(<ImportPreview />);
    expect(screen.getByRole("button", { name: "Importer" }).hasAttribute("disabled")).toBe(true);
  });
});
