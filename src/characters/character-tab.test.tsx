import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import type { IDockviewPanelProps } from "dockview-react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { GameConnectionStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { type CharacterView, useCharacters } from "./character-store.ts";
import { CharacterTab } from "./character-tab.tsx";

const setTitle = vi.fn();

// The asset protocol's URL of a file, as Tauri makes it; happy-dom has no Tauri.
vi.mock("@tauri-apps/api/core", async (original) => ({
  ...(await original<typeof import("@tauri-apps/api/core")>()),
  convertFileSrc: (path: string) => `asset://${path}`,
}));

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
  key: "294-1",
  name: "Lyra",
  level: 87,
  breed: 9,
  breedName: { fr: "Crâ", en: "Cra" },
  head: "data/heads/9-1.png",
  sex: "female",
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
    expect(screen.getByText("Level 87 · Cra · Exploring")).toBeTruthy();
    expect(screen.getByText("Amakna › Forest · 4, -18 · cell 288")).toBeTruthy();
  });

  it("names its breed in the interface's language", async () => {
    useCharacters.setState({ views: { 1: lyra } });
    await act(() => i18next.changeLanguage("fr"));
    try {
      renderTab();
      expect(screen.getByText("Niveau 87 · Crâ · Exploration")).toBeTruthy();
    } finally {
      await act(() => i18next.changeLanguage("en"));
    }
  });

  it("gives only the level while the data does not name the breed", () => {
    useCharacters.setState({ views: { 1: { ...lyra, breedName: null } } });
    renderTab();
    expect(screen.getByText("Level 87 · Exploring")).toBeTruthy();
  });

  it("shows its breed's head in place of its initials", () => {
    useCharacters.setState({ views: { 1: lyra } });
    const { container } = renderTab();
    expect(container.querySelector("img")?.getAttribute("src")).toBe("asset://data/heads/9-1.png");
    expect(screen.queryByText("Ly")).toBeNull();
  });

  it("shows its initials without a head, or when the head cannot be loaded", () => {
    useCharacters.setState({ views: { 1: { ...lyra, head: null } } });
    const { container, unmount } = renderTab();
    expect(container.querySelector("img")).toBeNull();
    expect(screen.getByText("Ly")).toBeTruthy();
    unmount();

    useCharacters.setState({ views: { 1: lyra } });
    const again = renderTab();
    const image = again.container.querySelector("img");
    expect(image).not.toBeNull();
    if (image !== null) {
      fireEvent.error(image);
    }
    expect(again.container.querySelector("img")).toBeNull();
    expect(screen.getByText("Ly")).toBeTruthy();
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
    act(() => useCharacters.setState({ views: { 1: lyra } }));
    expect(setTitle).toHaveBeenCalledWith("Lyra");
  });

  it("says a gauge it does not know, on screen and to screen readers", () => {
    useCharacters.setState({ views: { 1: { ...lyra, pods: null } } });
    renderTab();
    const pods = screen.getByRole("meter", { name: "Pods" });
    expect(pods.getAttribute("aria-valuetext")).toBe("—");
    expect(pods.parentElement?.textContent).toContain("—");
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
