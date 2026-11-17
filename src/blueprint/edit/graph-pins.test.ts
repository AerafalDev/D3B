import { describe, expect, it } from "vitest";

import type { GraphView_Serialize } from "@/bindings.ts";
import { pinFinder } from "./graph-pins.ts";

const pin = { id: "b", side: "in", carries: "number", wildcard: true, default: null } as const;
const node = (id: string, more: object) => ({
  id,
  does: "kind",
  kind: "greater_or_equal",
  family: "query",
  fromStep: null,
  pins: [pin],
  inputs: {},
  position: null,
  event: null,
  ...more,
});
const graph = {
  id: "events",
  name: "Events",
  function: false,
  inputs: [],
  outputs: [],
  wires: [],
  choices: [],
  nodes: [
    node("ge", { types: ["number", "duration"] }),
    node("timed", { options: { valueType: "time" } }),
    node("add", { kind: "test_add_to", options: { variable: "pods" } }),
  ],
} as unknown as GraphView_Serialize;

describe("a graph's pins for the drag", () => {
  it("carry their node's types and the type it notes, a kind changing a variable typed by its pins", () => {
    const pinOf = pinFinder(graph);
    expect(pinOf("ge", "b", "in")).toMatchObject({ types: ["number", "duration"], typed: null });
    expect(pinOf("timed", "b", "in")).toMatchObject({ types: [], typed: "time" });
    expect(pinOf("add", "b", "in")?.typed).toBe("number");
    expect([pinOf("ge", "b", "out"), pinOf("none", "b", "in")]).toEqual([null, null]);
  });
});
