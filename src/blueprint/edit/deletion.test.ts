import type { Edge } from "@xyflow/react";
import { describe, expect, it } from "vitest";

import { deletion, menuDeletion } from "./deletion.ts";

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
