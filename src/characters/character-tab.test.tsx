import { cleanup, render, screen } from "@testing-library/react";
import type { IDockviewPanelProps } from "dockview-react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { GameConnectionStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { type CharacterView, useCharacters } from "./character-store.ts";
import { CharacterTab } from "./character-tab.tsx";

const setTitle = vi.fn();

function protocol(on: GameConnectionStatus["protocol"]) {
  const connection = { number: 1, build: "3.7.13.17", protocol: on, understood: 0, read: 0, unknown: 0, notTrusted: 0 };
  useRelay.setState({
    status: {
      state: "ready",
      reason: null,
      clientBuild: null,
      gameConnections: 1,
      notUnderstood: 0,
      connections: [{ ...connection, dropped: 0, map: null }],
    },
  });
}

function renderTab() {
  // The tab reads its parameters and sets its title: the rest of dockview's props play no part.
  const props = { api: { setTitle }, params: { icon: "user", connection: 1 } } as unknown as IDockviewPanelProps<{
    icon: "user";
    connection: number;
  }>;
  return render(<CharacterTab {...props} />);
}

const lyra: CharacterView = {
  name: "Lyra",
  level: 87,
  breed: 10,
  fighting: false,
  place: {
    mapId: 1,
    at: { x: 4, y: -18 },
    subArea: { fr: "Forêt", en: "Forest" },
    area: { fr: "Amakna", en: "Amakna" },
  },
  cell: 288,
  life: { value: 450, max: 480 },
  lifeEstimated: true,
  pods: { value: 812, max: 2350 },
  kamas: 145_320,
  actionPoints: 7,
  movementPoints: 3,
  energy: { value: 9870, max: 10_000 },
  experience: { value: 200, max: 400 },
  actors: [
    { id: 7, kind: { kind: "player", name: "Mirabeau" }, cell: 150 },
    { id: -2, kind: { kind: "monsterGroup", monsters: [] }, cell: 190 },
  ],
  interactives: [],
  stated: [{ elementId: 465_210, cell: 287, state: 1 }],
  stale: { map: false, life: false, pods: false, kamas: false },
};

describe("a character's tab", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    setTitle.mockClear();
    useCharacters.setState({ views: {}, selected: null });
    protocol("on");
  });
  afterEach(cleanup);

  it("shows who and where the character is", () => {
    useCharacters.setState({ views: { 1: lyra } });
    renderTab();
    expect(screen.getByRole("heading", { name: "Lyra" })).toBeTruthy();
    expect(screen.getByText("Level 87 · class 10 · Exploring")).toBeTruthy();
    expect(screen.getByText("Amakna › Forest · 4, -18 · cell 288")).toBeTruthy();
  });

  it("shows its gauges, a life still regenerating marked so", () => {
    useCharacters.setState({ views: { 1: lyra } });
    renderTab();
    expect(screen.getByRole("meter", { name: "Life" }).getAttribute("value")).toBe("450");
    expect(screen.getByText("450 / 480")).toBeTruthy();
    expect(screen.getByText(/regenerating/)).toBeTruthy();
    expect(screen.getByText("812 / 2,350")).toBeTruthy();
  });

  it("counts what is around", () => {
    useCharacters.setState({ views: { 1: lyra } });
    renderTab();
    const around = screen.getByRole("region", { name: "Around" }).textContent;
    expect(around).toBe("AroundPlayers1Monster groups1NPCs0Resources1");
  });

  it("takes the character's name for its tab once known", () => {
    renderTab();
    expect(setTitle).not.toHaveBeenCalled();
    cleanup();
    useCharacters.setState({ views: { 1: lyra } });
    renderTab();
    expect(setTitle).toHaveBeenCalledWith("Lyra");
  });

  it("waits for the character to enter the game", () => {
    renderTab();
    expect(screen.getByText("Waiting for the character to enter the game…")).toBeTruthy();
  });

  it("says why a connection whose build has no pins shows nothing", () => {
    protocol("unmapped");
    useCharacters.setState({ views: { 1: lyra } });
    renderTab();
    expect(screen.getByText(/has no pins: its protocol is not read/)).toBeTruthy();
  });

  it("says when its connection has closed", () => {
    useRelay.setState({ status: null });
    renderTab();
    expect(screen.getByText("This connection has closed.")).toBeTruthy();
  });
});
