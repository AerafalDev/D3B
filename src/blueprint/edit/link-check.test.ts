import { describe, expect, it } from "vitest";

import type { PinCarries, PinSide } from "@/bindings.ts";
import { type LinkEnd, linkRefusal } from "./link-check.ts";

function end(node: string, side: PinSide, carries: PinCarries): LinkEnd {
  return { node, pin: { id: "pin", side, carries, default: null } };
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
});
