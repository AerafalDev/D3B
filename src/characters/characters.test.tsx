import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { CharacterChanged_Serialize, GameConnectionStatus_Serialize, RelayStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";

type Listener = (event: { payload: CharacterChanged_Serialize }) => void;

const backEnd = vi.hoisted(() => ({ listener: null as Listener | null }));

vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@/bindings.ts", () => ({
  events: {
    characterChanged: {
      listen: async (listener: Listener) => {
        backEnd.listener = listener;
        return () => {};
      },
    },
  },
}));

const { useCharacters } = await import("./character-store.ts");
const { CharacterList } = await import("./character-list.tsx");
const { CharacterView } = await import("./character-view.tsx");

type View = NonNullable<CharacterChanged_Serialize["view"]>;

const view: View = {
  name: "Lyra",
  level: 87,
  breed: 10,
  fighting: false,
  place: {
    mapId: 88_213_271,
    at: { x: 4, y: -18 },
    subArea: { fr: "Forêt", en: "Forest" },
    area: { fr: "Amakna", en: "Amakna" },
  },
  cell: 288,
  life: { value: 450, max: 480 },
  lifeEstimated: true,
  pods: { value: 812, max: 2_350 },
  kamas: 145_320,
  actionPoints: 7,
  movementPoints: 3,
  energy: { value: 9_870, max: 10_000 },
  experience: { value: 200, max: 400 },
  actors: [
    { id: 7, kind: { kind: "player", name: "Mirabeau" }, cell: 150 },
    {
      id: -2,
      kind: {
        kind: "monsterGroup",
        monsters: [
          { id: 489, grade: 3, level: 52 },
          { id: 101, grade: 1, level: 34 },
        ],
      },
      cell: 190,
    },
  ],
  interactives: [],
  stated: [{ elementId: 465_210, cell: 287, state: 1 }],
  stale: { map: false, life: false, pods: false, kamas: false },
};

function connection(
  number: number,
  protocol: GameConnectionStatus_Serialize["protocol"] = "on",
): GameConnectionStatus_Serialize {
  return {
    number,
    build: "3.7.13.17",
    protocol,
    understood: 0,
    read: 0,
    unknown: 0,
    notTrusted: 0,
    dropped: 0,
    map: null,
  };
}

function relay(connections: GameConnectionStatus_Serialize[]) {
  const status: RelayStatus = {
    state: "ready",
    reason: null,
    clientBuild: null,
    gameConnections: connections.length,
    notUnderstood: 0,
    connections,
  };
  useRelay.setState({ status, follow: () => () => {} });
}

async function changed(payload: CharacterChanged_Serialize) {
  await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());
  backEnd.listener?.({ payload });
}

describe("the characters", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    useCharacters.setState({ views: {}, selected: null });
    backEnd.listener = null;
  });
  afterEach(cleanup);

  it("keeps each connection's view until it closes, and forgets the selection with it", async () => {
    useCharacters.getState().follow();
    await changed({ connection: 1, view });
    useCharacters.getState().select(1);
    expect(useCharacters.getState().views[1]?.name).toBe("Lyra");

    await changed({ connection: 1, view: null });

    expect(useCharacters.getState().views).toEqual({});
    expect(useCharacters.getState().selected).toBeNull();
  });

  it("lists every connection, a character by its name and one before its selection by its number", async () => {
    relay([connection(1), connection(2), connection(3, "unmapped")]);
    render(<CharacterList />);
    await changed({ connection: 1, view });

    expect(await screen.findByText("Lyra")).toBeDefined();
    expect(screen.getByText("4,-18")).toBeDefined();
    expect(screen.getByText("Connection 2")).toBeDefined();
    expect(screen.getByText("protocol off (build without pins)")).toBeDefined();
  });

  it("shows the chosen character: where, its gauges, and what is around", async () => {
    relay([connection(1)]);
    useCharacters.setState({ views: { 1: view }, selected: 1, follow: () => () => {} });
    render(<CharacterView />);

    expect(screen.getByText("Map 4,-18 · cell 288")).toBeDefined();
    expect(screen.getByText("Forest · Amakna")).toBeDefined();
    expect(screen.getByText("(estimated, regenerating)")).toBeDefined();
    expect(screen.getByText("Mirabeau")).toBeDefined();
    expect(screen.getByText("Monster 489 + 1")).toBeDefined();
    expect(screen.getByText("lvl 34–52")).toBeDefined();
    expect(screen.getByText("harvested")).toBeDefined();
    fireEvent.mouseDown(screen.getByRole("tab", { name: "Protocol" }));
    expect(screen.getByRole("tab", { name: "Protocol" })).toBeDefined();
  });

  it("asks to choose a character when none is", () => {
    relay([connection(1)]);
    render(<CharacterView />);

    expect(screen.getByText("Choose a character to follow it.")).toBeDefined();
  });
});
