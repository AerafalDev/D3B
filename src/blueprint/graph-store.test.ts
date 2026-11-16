import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The back end and the notifications, faked: what an edit gives back, and what it says.
const backEnd = vi.hoisted(() => ({
  edit: vi.fn(),
  graphs: vi.fn(),
  undo: vi.fn(),
  redo: vi.fn(),
  toasts: [] as string[],
}));
vi.mock("@/bindings.ts", () => ({
  commands: {
    editBlueprint: backEnd.edit,
    blueprintGraphs: backEnd.graphs,
    undoBlueprint: backEnd.undo,
    redoBlueprint: backEnd.redo,
  },
}));
vi.mock("sonner", () => ({ toast: { error: (text: string) => backEnd.toasts.push(text) } }));

const { useGraphs } = await import("./graph-store.ts");

const MOVE: GraphEdit_Deserialize = { edit: "move", nodes: [{ node: "n1", position: [0, 0] }] };
const SAVED = { id: "mine", graphs: [], problems: [] };
const READ_AGAIN = { id: "mine", graphs: [], problems: [{ kind: "eventTwice" }] };

describe("an edit", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  beforeEach(() => {
    backEnd.toasts.length = 0;
    useGraphs.setState({ read: {} });
  });

  it("takes the graphs it gives back once saved", async () => {
    backEnd.edit.mockResolvedValueOnce(SAVED);
    expect(await useGraphs.getState().edit("mine", "events", MOVE)).toBe(true);
    expect(backEnd.edit).toHaveBeenCalledWith("mine", "events", MOVE);
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: SAVED });
  });

  it("says why it was not saved and reads the blueprint again", async () => {
    backEnd.edit.mockRejectedValueOnce({ kind: "notSaved", reason: "disque plein" });
    backEnd.graphs.mockResolvedValueOnce(READ_AGAIN);
    expect(await useGraphs.getState().edit("mine", "events", MOVE)).toBe(false);
    expect(backEnd.toasts).toEqual(["La modification n'est pas enregistrée : disque plein"]);
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: READ_AGAIN });
    backEnd.edit.mockRejectedValueOnce({ kind: "otherType" });
    backEnd.graphs.mockResolvedValueOnce(READ_AGAIN);
    await useGraphs.getState().edit("mine", "events", MOVE);
    expect(backEnd.toasts[1]).toBe("La modification n'est pas enregistrée : le blueprint est relu.");
  });
});

describe("undo and redo", () => {
  beforeEach(() => {
    backEnd.toasts.length = 0;
    useGraphs.setState({ read: {} });
  });

  it("take the graphs Rust wrote back", async () => {
    backEnd.undo.mockResolvedValueOnce(SAVED);
    expect(await useGraphs.getState().undo("mine")).toBe(true);
    expect(backEnd.undo).toHaveBeenCalledWith("mine");
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: SAVED });
    backEnd.redo.mockResolvedValueOnce(READ_AGAIN);
    expect(await useGraphs.getState().redo("mine")).toBe(true);
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: READ_AGAIN });
  });

  it("say why nothing was written and read the blueprint again", async () => {
    backEnd.undo.mockRejectedValueOnce({ kind: "notSaved", reason: "disque plein" });
    backEnd.graphs.mockResolvedValueOnce(READ_AGAIN);
    expect(await useGraphs.getState().undo("mine")).toBe(false);
    expect(backEnd.toasts).toEqual(["La modification n'est pas enregistrée : disque plein"]);
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: READ_AGAIN });
  });

  it("wait for the change sent before them, so Rust takes them in the order sent", async () => {
    let answer: (graphs: typeof SAVED) => void = () => {};
    backEnd.edit.mockReturnValueOnce(new Promise((resolve) => (answer = resolve)));
    backEnd.undo.mockReset().mockResolvedValue(READ_AGAIN);
    const edited = useGraphs.getState().edit("mine", "events", MOVE);
    const undone = useGraphs.getState().undo("mine");
    await Promise.resolve();
    expect(backEnd.undo, "not sent while the edit is under way").not.toHaveBeenCalled();
    answer(SAVED);
    await Promise.all([edited, undone]);
    expect(backEnd.undo).toHaveBeenCalledTimes(1);
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: READ_AGAIN });
  });

  it("go on after a change refused", async () => {
    backEnd.edit.mockRejectedValueOnce({ kind: "noSuchNode" });
    backEnd.graphs.mockResolvedValueOnce(SAVED);
    backEnd.undo.mockReset().mockResolvedValue(READ_AGAIN);
    const results = await Promise.all([
      useGraphs.getState().edit("mine", "events", MOVE),
      useGraphs.getState().undo("mine"),
    ]);
    expect(results).toEqual([false, true]);
    expect(useGraphs.getState().read["mine"]).toEqual({ state: "read", graphs: READ_AGAIN });
  });
});

describe("forget", () => {
  it("drops a blueprint deleted", () => {
    useGraphs.setState({ read: { mine: { state: "read", graphs: SAVED } } });
    useGraphs.getState().forget("mine");
    expect(useGraphs.getState().read["mine"]).toBeUndefined();
  });
});
