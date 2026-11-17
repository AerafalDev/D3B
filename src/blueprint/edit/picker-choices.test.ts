import { describe, expect, it } from "vitest";

import type { ChoiceView_Serialize, PinCarries, PinSide, PinView_Serialize } from "@/bindings.ts";
import type { LinkEnd } from "./link-check.ts";
import { pickerChoices } from "./picker-choices.ts";

function pin(id: string, side: PinSide, carries: PinCarries): PinView_Serialize {
  return { id, side, carries, list: false, ranges: false, default: null };
}

const RUN = pin("run", "in", "exec");
const THEN = pin("then", "out", "exec");

function kind(id: string, family: ChoiceView_Serialize["family"], pins: PinView_Serialize[]): ChoiceView_Serialize {
  return { does: "kind", kind: id, family, fromStep: null, pins };
}

const CHOICES: ChoiceView_Serialize[] = [
  kind("life_below", "event", [THEN, pin("threshold", "in", "percentage")]),
  kind("harvest_until", "action", [RUN, pin("done", "out", "exec"), pin("zone", "in", "zone")]),
  kind("notify", "action", [RUN, pin("done", "out", "exec"), pin("text", "in", "text")]),
  kind("if", "flow", [RUN, pin("condition", "in", "bool")]),
  kind("wait", "flow", [RUN, THEN, pin("minutes", "in", "number")]),
  kind("finish", "flow", [RUN]),
  kind("life_percent", "query", [pin("value", "out", "percentage")]),
  kind("add", "query", [pin("a", "in", "number"), pin("value", "out", "number")]),
  kind("at_least", "query", [pin("a", "in", "number"), pin("value", "out", "bool")]),
  { does: "get", variable: "zone", family: "variable", fromStep: null, pins: [pin("value", "out", "zone")] },
];

function from(side: PinSide, id: string, carries: PinCarries): LinkEnd {
  return { node: "n3", pin: pin(id, side, carries) };
}

const ids = (choices: ChoiceView_Serialize[]) =>
  choices.map((it) => (it.does === "kind" ? it.kind : it.does === "call" ? it.function : it.variable));

describe("pickerChoices", () => {
  it("puts first, after a Failed, notify, wait and finish", () => {
    const { often } = pickerChoices(CHOICES, from("out", "failed", "exec"), "");
    expect(ids(often)).toEqual(["notify", "wait", "finish"]);
  });

  it("puts first, after another flow, if, notify and wait", () => {
    expect(ids(pickerChoices(CHOICES, from("out", "done", "exec"), "").often)).toEqual(["if", "notify", "wait"]);
  });

  it("puts first, from a value, the nodes that take it, and for an input those that give it", () => {
    expect(ids(pickerChoices(CHOICES, from("out", "value", "percentage"), "").often)).toEqual([
      "life_below",
      "wait",
      "add",
    ]);
    expect(ids(pickerChoices(CHOICES, from("in", "zone", "zone"), "").often)).toEqual(["zone"]);
  });

  it("lists by family only what has a pin the dragged one fits, and everything without a drag", () => {
    const { all } = pickerChoices(CHOICES, from("out", "value", "percentage"), "");
    expect(all.map(([family, choices]) => [family, ids(choices)])).toEqual([
      ["event", ["life_below"]],
      ["flow", ["wait"]],
      ["query", ["add", "at_least"]],
    ]);
    const free = pickerChoices(CHOICES, null, "");
    expect(free.often).toEqual([]);
    expect(free.all.flatMap(([, choices]) => choices)).toHaveLength(CHOICES.length);
  });

  it("keeps, when searched, what its words hold, in either case and without accents", () => {
    const words = (choice: ChoiceView_Serialize) =>
      choice.does === "kind" && choice.kind === "notify" ? "Prévenir" : "";
    const found = pickerChoices(CHOICES, null, "PREVENIR", words);
    expect(found.all.flatMap(([, choices]) => ids(choices))).toEqual(["notify"]);
    expect(pickerChoices(CHOICES, null, "nothing at all", words).all).toEqual([]);
  });
});
