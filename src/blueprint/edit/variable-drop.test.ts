import { describe, expect, it } from "vitest";

import { draggedVariable, dropPoint, droppedAs } from "./variable-drop.ts";

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

  it("lands where the pointer began, moved by the drag", () => {
    const began = new MouseEvent("pointerdown", { clientX: 40, clientY: 60 });
    expect(dropPoint(began, { x: 300, y: -20 })).toEqual({ x: 340, y: 40 });
    expect(dropPoint(null, { x: 1, y: 1 })).toBeNull();
    expect(dropPoint(new Event("keydown"), { x: 1, y: 1 })).toBeNull();
  });

  it("carries a variable's id, and nothing else dragged", () => {
    expect(draggedVariable({ variable: "pods" })).toBe("pods");
    for (const other of [undefined, null, {}, { variable: "" }, { variable: 3 }]) {
      expect(draggedVariable(other)).toBeNull();
    }
  });
});
