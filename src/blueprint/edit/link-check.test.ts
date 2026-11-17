import { describe, expect, it } from "vitest";

import type { PinCarries, PinSide } from "@/bindings.ts";
import { type LinkEnd, linkRefusal } from "./link-check.ts";

function end(node: string, side: PinSide, carries: PinCarries, more: Partial<LinkEnd["pin"]> = {}): LinkEnd {
  return { node, pin: { id: "pin", side, carries, default: null, ...more } };
}

/** An untyped `≥`'s input: a wildcard reading numbers, open to its four types. */
function comparison(typed: string | null = null, carries: PinCarries = "number"): LinkEnd {
  const types = ["number", "percentage", "duration", "time"];
  return { ...end("ge", "in", carries, { wildcard: true }), types, typed };
}

describe("linkRefusal", () => {
  it("takes the flow into the flow and a value into its type, whichever end the drag began from", () => {
    expect(linkRefusal(end("a", "out", "exec"), end("b", "in", "exec"))).toBeNull();
    expect(linkRefusal(end("a", "out", "zone"), end("b", "in", "zone"))).toBeNull();
    expect(linkRefusal(end("a", "in", "zone"), end("b", "out", "zone"))).toBeNull();
  });

  it("lets a percentage feed a number, not the other way", () => {
    expect(linkRefusal(end("a", "out", "percentage"), end("b", "in", "number"))).toBeNull();
    expect(linkRefusal(end("a", "out", "number"), end("b", "in", "percentage"))).toBe("otherType");
    expect(linkRefusal(end("a", "in", "number"), end("b", "out", "percentage"))).toBeNull();
    expect(linkRefusal(end("a", "in", "percentage"), end("b", "out", "number"))).toBe("otherType");
  });

  it("refuses another type, the flow into a value, two ends of one side and a node to itself", () => {
    expect(linkRefusal(end("a", "out", "zone"), end("b", "in", "bank"))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "exec"), end("b", "in", "bool"))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "exec"), end("b", "out", "exec"))).toBe("wrongWay");
    expect(linkRefusal(end("a", "out", "exec"), end("a", "in", "exec"))).toBe("sameNode");
  });

  it("takes a list into a list only, ranges into ranges, and a list of items into the items", () => {
    const zones = end("a", "out", "zone", { list: true });
    expect(linkRefusal(zones, end("b", "in", "zone", { list: true }))).toBeNull();
    expect(linkRefusal(zones, end("b", "in", "zone"))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "zone"), end("b", "in", "zone", { list: true }))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "zone", { ranges: true }), end("b", "in", "zone"))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "item", { list: true }), end("b", "in", "items"))).toBeNull();
    expect(linkRefusal(end("a", "out", "items"), end("b", "in", "item", { list: true }))).toBe("otherType");
  });

  it("takes any value into a Text's hole, but neither the flow nor a wildcard of no type", () => {
    expect(linkRefusal(end("a", "out", "zone", { list: true }), end("b", "in", "any"))).toBeNull();
    expect(linkRefusal(end("a", "out", "exec"), end("b", "in", "any"))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "wildcard", { wildcard: true }), end("b", "in", "any"))).toBe("otherType");
  });

  it("lets a wildcard of no type take a value of a type and shape its kind takes, never another wildcard", () => {
    expect(linkRefusal(end("a", "out", "duration"), comparison())).toBeNull();
    expect(linkRefusal(end("a", "out", "time"), comparison())).toBeNull();
    expect(linkRefusal(end("a", "out", "zone"), comparison())).toBe("otherType");
    expect(linkRefusal(end("a", "out", "duration", { list: true }), comparison())).toBe("otherType");
    const any = end("e", "out", "wildcard", { wildcard: true });
    expect(linkRefusal(any, end("b", "in", "wildcard", { wildcard: true }))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "map"), end("b", "in", "wildcard", { wildcard: true }))).toBeNull();
  });

  it("reads a typed wildcard as its type and an untyped comparison as numbers too", () => {
    expect(linkRefusal(end("a", "out", "percentage"), comparison())).toBeNull();
    expect(linkRefusal(end("a", "out", "duration"), comparison("time", "time"))).toBe("otherType");
    expect(linkRefusal(end("a", "out", "time"), comparison("time", "time"))).toBeNull();
  });
});
