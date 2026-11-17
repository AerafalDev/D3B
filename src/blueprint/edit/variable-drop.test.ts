import { describe, expect, it } from "vitest";

import { draggedVariable, droppedAs, variableDropped } from "./variable-drop.ts";

const keys = (more: object = {}) => ({ ctrlKey: false, metaKey: false, altKey: false, ...more });

describe("a variable dropped on the graph", () => {
  it("is read with Ctrl or Cmd, written with Alt, and asked otherwise", () => {
    expect(droppedAs(keys({ ctrlKey: true }))).toBe("get");
    expect(droppedAs(keys({ metaKey: true }))).toBe("get");
    expect(droppedAs(keys({ altKey: true }))).toBe("set");
    expect(droppedAs(keys())).toBe("ask");
  });

  it("asks when both keys say two things", () => {
    expect(droppedAs(keys({ ctrlKey: true, altKey: true }))).toBe("ask");
  });

  it("adds a node where the pointer last was, or asks there, and does nothing off the graph or in one of D3B's", () => {
    const toFlow = ({ x, y }: { x: number; y: number }) => ({ x: x / 2 + 0.4, y: y / 2 - 0.6 });
    const at = (more: object = {}) => ({ ...keys(more), x: 100, y: 40 });
    const pods = { variable: "pods" };
    expect(variableDropped(pods, true, true, at({ altKey: true }), toFlow)).toEqual({
      kind: "add",
      edit: { edit: "add", node: { does: "set", variable: "pods" }, position: [50, 19], wiredTo: null },
    });
    expect(variableDropped(pods, true, true, at({ ctrlKey: true }), toFlow)).toMatchObject({
      edit: { node: { does: "get", variable: "pods" } },
    });
    expect(variableDropped(pods, true, true, at(), toFlow)).toEqual({
      kind: "ask",
      variable: "pods",
      client: { x: 100, y: 40 },
      at: { x: 50.4, y: 19.4 },
    });
    expect(variableDropped(pods, false, true, at(), toFlow), "off the graph").toBeNull();
    expect(variableDropped(pods, true, false, at(), toFlow), "one of D3B's").toBeNull();
    expect(variableDropped({ node: "n1" }, true, true, at(), toFlow), "not a variable").toBeNull();
    expect(variableDropped(pods, true, true, null, toFlow), "no pointer seen").toBeNull();
  });

  it("carries a variable's id, and nothing else dragged", () => {
    expect(draggedVariable({ variable: "pods" })).toBe("pods");
    for (const other of [undefined, null, {}, { variable: "" }, { variable: 3 }]) {
      expect(draggedVariable(other)).toBeNull();
    }
  });
});
