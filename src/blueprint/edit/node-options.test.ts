import { describe, expect, it } from "vitest";

import type { GraphNode } from "../node-sentence.ts";
import { addedPin, optionPin, pinRemoval } from "./node-options.ts";

const pin = (id: string) => ({ id, side: "out", carries: "exec", default: null }) as GraphNode["pins"][number];

const sequence = {
  id: "seq",
  does: "kind",
  kind: "sequence",
  family: "flow",
  fromStep: null,
  pins: [pin("then_1"), pin("then_2"), pin("then_3")],
  inputs: {},
  position: [0, 0],
  event: null,
  options: { count: 3 },
  adds: { pin: "then", side: "out", least: 2 },
} as GraphNode;

const switchOf = (cases: string[]): GraphNode =>
  ({
    ...sequence,
    id: "sw",
    kind: "switch",
    pins: [...cases.map((_, at) => pin(`case_${at + 1}`)), pin("otherwise")],
    options: { valueType: "job", cases },
    adds: null,
    takesCases: true,
  }) as unknown as GraphNode;

describe("a node's options", () => {
  it("adds an output after the last, from the kind's least when the node gives no count", () => {
    expect(addedPin(sequence)).toEqual({ edit: "options", node: "seq", count: 4 });
    expect(addedPin({ ...sequence, options: {} })).toEqual({ edit: "options", node: "seq", count: 3 });
  });

  it("adds none on a node that numbers no pin, nor past 255", () => {
    expect(addedPin(switchOf(["24"]))).toBeNull();
    expect(addedPin({ ...sequence, options: { count: 255 } })).toBeNull();
  });

  it("removes a numbered output above the kind's least, and any case", () => {
    expect(pinRemoval(sequence, "then_2")).toEqual({ edit: "removePin", node: "seq", pin: "then_2" });
    expect(pinRemoval({ ...sequence, options: {} }, "then_1")).toBeNull();
    expect(pinRemoval(switchOf(["24"]), "case_1")).toEqual({ edit: "removePin", node: "sw", pin: "case_1" });
  });

  it("says which pins its options make, whose menu is Remove, at the least too", () => {
    expect([optionPin(sequence, "then_1"), optionPin({ ...sequence, options: {} }, "then_2")]).toEqual([true, true]);
    expect([optionPin(sequence, "run"), optionPin(switchOf(["24"]), "otherwise")]).toEqual([false, false]);
    expect(optionPin(switchOf(["24"]), "case_1")).toBe(true);
  });

  it("removes nothing else: the flow in, Otherwise, a pin past the count, a written-out number", () => {
    for (const pinId of ["run", "then_4", "then_02", "then_x"]) {
      expect(pinRemoval(sequence, pinId)).toBeNull();
    }
    expect(pinRemoval(switchOf(["24"]), "otherwise")).toBeNull();
    expect(pinRemoval(switchOf(["24"]), "case_2")).toBeNull();
  });
});
