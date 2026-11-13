import { beforeEach, describe, expect, it, vi } from "vitest";

import type { FrameSummary_Serialize, InspectorFrames_Serialize } from "@/bindings.ts";

type Listener = (event: { payload: InspectorFrames_Serialize }) => void;

const backEnd = vi.hoisted(() => ({
  listener: null as Listener | null,
  watched: [] as (number | null)[],
  answers: [] as ((frames: FrameSummary_Serialize[]) => void)[],
  unlisten: vi.fn(),
}));

vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    inspectorWatch: (connection: number | null) =>
      new Promise<FrameSummary_Serialize[]>((resolve) => {
        backEnd.watched.push(connection);
        backEnd.answers.push(resolve);
      }),
  },
  events: {
    inspectorFrames: {
      listen: async (listener: Listener) => {
        backEnd.listener = listener;
        return backEnd.unlisten;
      },
    },
  },
}));

const { KEPT, useInspector } = await import("./inspector-store.ts");

function frame(number: number): FrameSummary_Serialize {
  return {
    number,
    millis: number * 10,
    fromClient: false,
    name: "jow",
    readable: "MapCurrentEvent",
    outcome: "understood",
    size: 11,
  };
}

function frames(from: number, to: number): FrameSummary_Serialize[] {
  return Array.from({ length: to - from + 1 }, (_, index) => frame(from + index));
}

function numbers(): number[] {
  return useInspector.getState().frames.map((one) => one.number);
}

describe("useInspector", () => {
  beforeEach(() => {
    useInspector.setState({ connection: null, frames: [] });
    backEnd.listener = null;
    backEnd.watched = [];
    backEnd.answers = [];
  });

  it("watches none as it starts, so a reload leaves nothing streamed", async () => {
    useInspector.getState().follow();

    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());
    expect(backEnd.watched).toEqual([null]);
  });

  it("takes the frames kept, then those streamed for the connection watched", async () => {
    useInspector.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());

    useInspector.getState().watch(3);
    backEnd.answers.at(-1)?.(frames(1, 2));
    await vi.waitFor(() => expect(numbers()).toEqual([1, 2]));
    backEnd.listener?.({ payload: { connection: 3, frames: frames(3, 4), skipped: 0 } });
    backEnd.listener?.({ payload: { connection: 4, frames: frames(9, 9), skipped: 0 } });

    expect(numbers()).toEqual([1, 2, 3, 4]);
    expect(useInspector.getState().connection).toBe(3);
  });

  it("keeps frames streamed before the ones kept arrive, in order", async () => {
    useInspector.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());

    useInspector.getState().watch(3);
    backEnd.listener?.({ payload: { connection: 3, frames: frames(3, 4), skipped: 0 } });
    backEnd.answers.at(-1)?.(frames(1, 3));

    await vi.waitFor(() => expect(numbers()).toEqual([1, 2, 3, 4]));
  });

  it("drops the answer of a connection watched no more", async () => {
    useInspector.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());

    useInspector.getState().watch(3);
    const late = backEnd.answers.at(-1);
    useInspector.getState().watch(5);
    backEnd.answers.at(-1)?.(frames(7, 7));
    late?.(frames(1, 2));

    await vi.waitFor(() => expect(numbers()).toEqual([7]));
    expect(useInspector.getState().connection).toBe(5);
  });

  it("keeps its frames when the connection watched is watched again", () => {
    useInspector.setState({ connection: 3, frames: frames(1, 2) });

    useInspector.getState().watch(3);

    expect(numbers()).toEqual([1, 2]);
    expect(backEnd.watched).toEqual([]);
  });

  it("forgets the frames once it watches none", async () => {
    useInspector.setState({ connection: 3, frames: frames(1, 2) });

    useInspector.getState().watch(null);

    expect(useInspector.getState()).toMatchObject({ connection: null, frames: [] });
  });

  it("keeps the last frames only, as many as the back end keeps", async () => {
    useInspector.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());
    useInspector.getState().watch(3);
    backEnd.answers.at(-1)?.(frames(1, KEPT));
    await vi.waitFor(() => expect(numbers()).toHaveLength(KEPT));

    backEnd.listener?.({ payload: { connection: 3, frames: frames(KEPT + 1, KEPT + 5), skipped: 0 } });

    expect(numbers()).toHaveLength(KEPT);
    expect(numbers()[0]).toBe(6);
    expect(numbers().at(-1)).toBe(KEPT + 5);
  });
});
