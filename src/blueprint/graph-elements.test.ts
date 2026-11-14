import i18next from "i18next";
import { beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { graphElements, UNPLACED_STEP, UNPLACED_X } from "./graph-elements.ts";
import type { GraphNode } from "./node-sentence.ts";

const pin = (id: string, side: "in" | "out", carries: string) =>
  ({ id, side, carries, default: null }) as GraphNode["pins"][number];

const node = (id: string, more: object): GraphNode =>
  ({
    id,
    does: "kind",
    kind: "go_to_zone",
    family: "action",
    fromStep: 4,
    pins: [],
    inputs: {},
    position: null,
    event: null,
    ...more,
  }) as GraphNode;

const graph = {
  id: "events",
  name: "Event graph",
  function: false,
  nodes: [
    node("go", { position: [240, 0], pins: [pin("run", "in", "exec"), pin("zone", "in", "zone")] }),
    node("zone", {
      does: "get",
      variable: "zone",
      family: "variable",
      fromStep: null,
      pins: [pin("value", "out", "zone")],
    }),
    node("start", { kind: "on_start", family: "event", fromStep: null, pins: [pin("then", "out", "exec")] }),
  ],
  wires: [
    { from: ["start", "then"], to: ["go", "run"] },
    { from: ["zone", "value"], to: ["go", "zone"] },
  ],
} as never;

describe("a graph's elements", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  it("puts each node where the file says, the others one under the other on the left", () => {
    const { nodes } = graphElements(graph, new Map([["zone", "Zone"]]));
    expect(nodes.map((one) => [one.id, one.position.x, one.position.y])).toEqual([
      ["go", 240, 0],
      ["zone", UNPLACED_X, 0],
      ["start", UNPLACED_X, UNPLACED_STEP],
    ]);
  });

  it("draws a get as a pill with its variable's name and type, the others as boxes", () => {
    const { nodes } = graphElements(graph, new Map([["zone", "Zone"]]));
    expect(nodes.map((one) => one.type)).toEqual(["box", "pill", "box"]);
    expect(nodes[1]?.data).toEqual({ name: "Zone", valueType: "zone" });
  });

  it("joins each wire's ends by their pins, classed by what its source carries", () => {
    const { edges } = graphElements(graph, new Map());
    expect(
      edges.map((edge) => [edge.source, edge.sourceHandle, edge.target, edge.targetHandle, edge.className]),
    ).toEqual([
      ["start", "then", "go", "run", "blueprint-type-exec"],
      ["zone", "value", "go", "zone", "blueprint-type-zone"],
    ]);
  });
});
