import { describe, expect, it } from "vitest";

import { droppedAs } from "./variable-drop.ts";

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
});
