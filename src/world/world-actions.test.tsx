import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize, AtlasHint_Serialize, HintKind } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

vi.mock("@tauri-apps/api/core", () => ({ convertFileSrc: (path: string) => `asset://${path}` }));

const { WorldActions } = await import("./world-actions.tsx");
const { useWorld } = await import("./world-store.ts");
const { HIDDEN_KINDS } = await import("./hint-layout.ts");

const hint = (id: number, kind: HintKind, picture: string | null): AtlasHint_Serialize => ({
  id,
  kind,
  name: { fr: "Indice", en: "Hint" },
  at: [0, 0],
  level: 0,
  picture,
});

const BASE = { origin: [0, 0], mapSize: [10, 10], size: [100, 100], minScale: 0.2, maxScale: 1, startScale: 0.5 };
const ATLAS = {
  worlds: [
    {
      ...BASE,
      id: 1,
      name: { fr: "Monde des Douze", en: "World of Twelve" },
      zooms: [],
      // The first zaap lacks its picture: the second's stands for the kind.
      hints: [hint(1, "zaap", null), hint(2, "zaap", "410.png"), hint(3, "passage", "900.png")],
    },
    { ...BASE, id: 2, name: { fr: "Incarnam", en: "Incarnam" }, zooms: [], hints: [] },
  ],
  areas: [],
} as unknown as Atlas_Serialize;

/** Opens the hints' menu as a click does. */
function open() {
  fireEvent.pointerDown(screen.getByRole("button", { name: /Indices/ }), {
    button: 0,
    ctrlKey: false,
    pointerType: "mouse",
  });
}

describe("the World tab's hints", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    useWorld.setState({ read: { state: "read", atlas: ATLAS }, world: 1, hidden: HIDDEN_KINDS });
  });
  afterEach(cleanup);

  it("lists the kinds the world holds with their counts and a picture, the passages off at first", () => {
    render(<WorldActions />);
    open();
    const switches = screen.getAllByRole("menuitemcheckbox");
    expect(switches.map((one) => [one.textContent, one.getAttribute("aria-checked")])).toEqual([
      ["Zaaps 2", "true"],
      ["Passages 1", "false"],
    ]);
    expect(switches[0]?.querySelector("img")?.getAttribute("src")).toBe("asset://410.png");
  });

  it("switches a kind, all or none, the menu staying open", () => {
    render(<WorldActions />);
    open();
    fireEvent.click(screen.getByRole("menuitemcheckbox", { name: /Zaaps/ }));
    expect(useWorld.getState().hidden.has("zaap")).toBe(true);
    fireEvent.click(screen.getByRole("menuitem", { name: "Tout montrer" }));
    expect(useWorld.getState().hidden.size).toBe(0);
    fireEvent.click(screen.getByRole("menuitem", { name: "Tout cacher" }));
    expect(useWorld.getState().hidden.size).toBe(9);
    expect(screen.getByRole("menuitemcheckbox", { name: /Passages/ })).toBeTruthy();
  });

  it("has no hints' menu for a world without hints", () => {
    useWorld.setState({ world: 2 });
    render(<WorldActions />);
    expect(screen.queryByRole("button", { name: /Indices/ })).toBeNull();
    expect(screen.getByRole("combobox", { name: "Monde affiché" })).toBeTruthy();
  });
});
