import { beforeEach, describe, expect, it, vi } from "vitest";

import type { CharacterChanged_Serialize } from "@/bindings.ts";

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

type View = NonNullable<CharacterChanged_Serialize["view"]>;

const view: View = {
  name: "Lyra",
  level: 87,
  breed: 10,
  fighting: false,
  place: null,
  cell: null,
  life: { value: 450, max: 480 },
  lifeEstimated: false,
  pods: null,
  kamas: null,
  actionPoints: null,
  movementPoints: null,
  energy: null,
  experience: null,
  actors: [],
  interactives: [],
  stated: [],
  stale: { map: false, life: false, pods: false, kamas: false },
};

async function changed(payload: CharacterChanged_Serialize) {
  await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());
  backEnd.listener?.({ payload });
}

describe("the characters' store", () => {
  beforeEach(() => {
    useCharacters.setState({ views: {}, selected: null });
    backEnd.listener = null;
  });

  it("keeps each connection's view until it closes, and forgets the selection with it", async () => {
    const stop = useCharacters.getState().follow();
    await changed({ connection: 1, view });
    await changed({ connection: 2, view: { ...view, name: "Kordo" } });
    useCharacters.getState().select(1);

    await changed({ connection: 2, view: null });
    expect(useCharacters.getState().views[1]?.name).toBe("Lyra");
    expect(useCharacters.getState().selected).toBe(1);

    await changed({ connection: 1, view: null });
    expect(useCharacters.getState().views).toEqual({});
    expect(useCharacters.getState().selected).toBeNull();

    // Once stopped, a change heard late is not kept.
    stop();
    backEnd.listener?.({ payload: { connection: 3, view } });
    expect(useCharacters.getState().views).toEqual({});
  });
});
