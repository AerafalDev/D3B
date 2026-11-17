import { describe, expect, it } from "vitest";

import type { GraphNode } from "../node-sentence.ts";
import { onPin, pinRole } from "./summary-pins.ts";

const out = (id: string) => ({ id, side: "out", carries: "exec", default: null });
const node = (pins: string[], more: object = {}) =>
  ({ id: "n", pins: [{ ...out("run"), side: "in" }, ...pins.map(out)], ...more }) as unknown as GraphNode;

const sequence = node(["then_1", "then_2", "then_3"], { adds: { pin: "then", side: "out", least: 2 } });
const switched = node(["case_1", "case_2", "otherwise"], { takesCases: true });

describe("each flow output's role in the Summary", () => {
  it("goes on by Then or Done, and by a Sequence's last output", () => {
    expect(onPin(node(["done", "failed"]))).toBe("done");
    expect(onPin(node(["then"]))).toBe("then");
    expect(onPin(sequence)).toBe("then_3");
    expect(onPin(node(["true", "false"]))).toBeUndefined();
  });

  it("titles a branch, holds a Sequence's other outputs as sub-steps, lists a Switch's outputs on one line", () => {
    expect([pinRole(node(["true"]), "true"), pinRole(node(["failed"]), "failed")]).toEqual(["titled", "titled"]);
    expect([pinRole(sequence, "then_1"), pinRole(sequence, "then_2")]).toEqual(["untitled", "untitled"]);
    expect([pinRole(switched, "case_2"), pinRole(switched, "otherwise")]).toEqual(["listed", "listed"]);
  });

  it("titles any other output, a function's named exit, by its label", () => {
    expect(pinRole(node(["done", "full"]), "full")).toBe("titled");
  });
});
