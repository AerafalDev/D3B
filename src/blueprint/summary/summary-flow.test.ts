import { describe, expect, it } from "vitest";

import type { GraphView_Serialize } from "@/bindings.ts";
import { type SummaryFlow, summaryFlow } from "./summary-flow.ts";

const exec = (id: string, side: "in" | "out") => ({ id, side, carries: "exec", default: null });
const action = (id: string) => ({ id, pins: [exec("run", "in"), exec("done", "out"), exec("failed", "out")] });

function graph(nodes: { id: string; pins: object[] }[], wires: [string, string, string][]): GraphView_Serialize {
  return {
    id: "events",
    name: "Event graph",
    function: false,
    inputs: [],
    outputs: [],
    nodes: nodes.map((node) => ({ does: "kind", kind: "x", family: "action", fromStep: null, inputs: {}, ...node })),
    wires: wires.map(([from, pin, to]) => ({ from: [from, pin], to: [to, "run"] })),
  } as unknown as GraphView_Serialize;
}

/** A flow as lines: "label node", "label.branch:pin", "→ jump". */
function lines(flow: SummaryFlow): string[] {
  return [
    ...flow.steps.flatMap((step) => [
      `${step.label} ${step.node.id}`,
      ...step.branches.flatMap((branch) => [`${step.label}:${branch.pin}`, ...lines(branch.flow)]),
    ]),
    ...(flow.jump === null ? [] : [`${flow.jump.back ? "back" : "on"} ${flow.jump.to}`]),
  ];
}

const start = { id: "start", pins: [exec("then", "out")] };

describe("the Summary's flow", () => {
  it("numbers the steps an event runs and says a loop as back to its step", () => {
    const loop = graph(
      [start, action("go"), action("harvest"), action("bank")],
      [
        ["start", "then", "go"],
        ["go", "done", "harvest"],
        ["harvest", "done", "bank"],
        ["bank", "done", "harvest"],
      ],
    );
    expect(lines(summaryFlow(loop, "start", "then"))).toEqual(["1 go", "2 harvest", "3 bank", "back 2"]);
  });

  it("numbers a Failed's steps under its step, and a branch that joins later steps goes on to them", () => {
    const failing = graph(
      [start, action("bank"), action("notify"), action("deposit")],
      [
        ["start", "then", "bank"],
        ["bank", "done", "deposit"],
        ["bank", "failed", "notify"],
        ["notify", "done", "deposit"],
      ],
    );
    expect(lines(summaryFlow(failing, "start", "then"))).toEqual([
      "1 bank",
      "1:failed",
      "1.1 notify",
      "on 2",
      "2 deposit",
    ]);
  });

  it("says an If's two outputs as branches, which end its chain", () => {
    const choice = {
      id: "if",
      pins: [exec("run", "in"), exec("true", "out"), exec("false", "out")],
    };
    const branching = graph(
      [start, action("go"), choice, action("finish")],
      [
        ["start", "then", "go"],
        ["go", "done", "if"],
        ["if", "true", "finish"],
        ["if", "false", "go"],
      ],
    );
    expect(lines(summaryFlow(branching, "start", "then"))).toEqual([
      "1 go",
      "2 if",
      "2:true",
      "2.1 finish",
      "2:false",
      "back 1",
    ]);
  });

  it("counts an If's False on from its True, and numbers a branch's branch under it", () => {
    const choice = { id: "if", pins: [exec("run", "in"), exec("true", "out"), exec("false", "out")] };
    const both = graph(
      [start, choice, action("harvest"), action("bank"), action("notify"), action("retry")],
      [
        ["start", "then", "if"],
        ["if", "true", "harvest"],
        ["harvest", "done", "bank"],
        ["bank", "failed", "retry"],
        ["if", "false", "notify"],
        ["notify", "done", "harvest"],
      ],
    );
    expect(lines(summaryFlow(both, "start", "then"))).toEqual([
      "1 if",
      "1:true",
      "1.1 harvest",
      "1.2 bank",
      "1.2:failed",
      "1.2.1 retry",
      "1:false",
      "1.3 notify",
      "back 1.1",
    ]);
  });

  it("says a Failed linked back to its own step as back to it", () => {
    const retry = graph(
      [start, action("bank")],
      [
        ["start", "then", "bank"],
        ["bank", "failed", "bank"],
      ],
    );
    expect(lines(summaryFlow(retry, "start", "then"))).toEqual(["1 bank", "1:failed", "back 1"]);
  });

  it("ends a chain at a wire to no node", () => {
    expect(lines(summaryFlow(graph([start], [["start", "then", "gone"]]), "start", "then"))).toEqual([]);
  });

  it("has no step when the output is linked to nothing", () => {
    expect(lines(summaryFlow(graph([start], []), "start", "then"))).toEqual([]);
  });
});
