import type { Edge } from "@xyflow/react";
import { describe, expect, it } from "vitest";

import type { GraphNode } from "../node-sentence.ts";
import { deletion, menuDeletion, rightClicked } from "./deletion.ts";

function edge(from: [string, string], to: [string, string], selected = false): Edge {
  return {
    id: `${from.join(".")}>${to.join(".")}`,
    source: from[0],
    sourceHandle: from[1],
    target: to[0],
    targetHandle: to[1],
    selected,
  };
}

const START = edge(["n1", "then"], ["n3", "run"]);
const ZONE = edge(["n2", "value"], ["n3", "zone"], true);

describe("deletion", () => {
  it("names the nodes and the wires by their ends, as the IPC takes them", () => {
    expect(deletion(["n3"], [START, ZONE])).toEqual({
      edit: "delete",
      nodes: ["n3"],
      wires: [
        { from: ["n1", "then"], to: ["n3", "run"] },
        { from: ["n2", "value"], to: ["n3", "zone"] },
      ],
    });
  });

  it("is nothing when nothing is chosen", () => {
    expect(deletion([], [])).toBeNull();
  });
});

describe("menuDeletion", () => {
  it("deletes nothing from a pin's menu, which removes the pin", () => {
    expect(menuDeletion({ kind: "pin", id: "n1", pin: "then_2" }, [{ id: "n1" }], [])).toBeNull();
  });

  const nodes = [
    { id: "n1", selected: true },
    { id: "n2", selected: true },
    { id: "n3", selected: false },
  ];

  it("deletes the whole selection from a node that is in it, as Unreal does", () => {
    expect(menuDeletion({ kind: "node", id: "n1" }, nodes, [START, ZONE])).toEqual(deletion(["n1", "n2"], [ZONE]));
  });

  it("deletes only the node right-clicked outside the selection, and only the wire right-clicked", () => {
    expect(menuDeletion({ kind: "node", id: "n3" }, nodes, [START, ZONE])).toEqual(deletion(["n3"], []));
    expect(menuDeletion({ kind: "wire", id: START.id }, nodes, [START, ZONE])).toEqual(deletion([], [START]));
  });

  it("is nothing for a node or a wire the graph no longer draws", () => {
    expect(menuDeletion({ kind: "node", id: "n9" }, nodes, [])).toBeNull();
    expect(menuDeletion({ kind: "wire", id: "gone" }, nodes, [START])).toBeNull();
  });
});

describe("rightClicked", () => {
  const and = {
    id: "n4",
    adds: { pin: "in", side: "in", least: 2 },
    takesCases: false,
    options: { count: 3 },
  } as unknown as GraphNode;

  function handle(id: string): Element {
    const row = document.createElement("div");
    row.className = "blueprint-row";
    const dot = document.createElement("div");
    dot.className = "react-flow__handle";
    dot.setAttribute("data-handleid", id);
    const label = document.createElement("span");
    row.append(dot, label);
    return label;
  }

  it("lands on a numbered input of a compact value, its row or its dot, as on a box's", () => {
    expect(rightClicked(handle("in_2"), { id: "n4", type: "value", data: { node: and } })).toEqual({
      kind: "pin",
      id: "n4",
      pin: "in_2",
    });
    expect(rightClicked(handle("in_2"), { id: "n4", type: "box", data: { node: and } })).toEqual({
      kind: "pin",
      id: "n4",
      pin: "in_2",
    });
  });

  it("lands on the node for a pin that is not numbered, a pill, or no element", () => {
    expect(rightClicked(handle("value"), { id: "n4", type: "value", data: { node: and } })).toEqual({
      kind: "node",
      id: "n4",
    });
    expect(rightClicked(handle("in_2"), { id: "n5", type: "pill", data: { name: "Zone" } })).toEqual({
      kind: "node",
      id: "n5",
    });
    expect(rightClicked(null, { id: "n4", type: "value", data: { node: and } })).toEqual({ kind: "node", id: "n4" });
  });
});
