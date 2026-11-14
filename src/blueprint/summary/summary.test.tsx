import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import type { GraphView_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { Summary } from "./summary.tsx";

const pin = (id: string, side: "in" | "out", carries = "exec", fallback: string | null = null) => ({
  id,
  side,
  carries,
  default: fallback,
});
const flow = [pin("run", "in"), pin("done", "out"), pin("failed", "out")];

const node = (id: string, kind: string, more: object = {}) => ({
  id,
  does: "kind",
  kind,
  family: "action",
  fromStep: null,
  pins: flow,
  inputs: {},
  position: [0, 0],
  event: null,
  ...more,
});

const events = {
  id: "events",
  name: "Event graph",
  function: false,
  inputs: [],
  outputs: [],
  nodes: [
    node("life", "life_below", {
      family: "event",
      pins: [pin("then", "out"), pin("threshold", "in", "percentage", "40")],
      event: { priority: 2, mode: "interruptThenResume" },
    }),
    node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
    node("go", "go_to_zone", { fromStep: 4, pins: [...flow, pin("zone", "in", "zone")] }),
    node("bank", "call", { does: "call", function: "bank", family: "call" }),
    node("wait", "wait", {
      family: "flow",
      pins: [pin("run", "in"), pin("then", "out"), pin("minutes", "in", "number", "5")],
    }),
  ],
  wires: [
    { from: ["start", "then"], to: ["go", "run"] },
    { from: ["go", "done"], to: ["bank", "run"] },
    { from: ["bank", "done"], to: ["go", "run"] },
    { from: ["bank", "failed"], to: ["wait", "run"] },
  ],
};
const bank = {
  id: "bank",
  name: "Bank",
  function: true,
  inputs: [],
  outputs: [],
  nodes: [
    node("entry", "entry", { family: "function", pins: [pin("then", "out")] }),
    node("finish", "finish", { family: "flow", pins: [pin("run", "in")] }),
  ],
  wires: [{ from: ["entry", "then"], to: ["finish", "run"] }],
};
const graphs = [events, bank] as unknown as GraphView_Serialize[];
const names = new Map([["bank", "Vider à la banque"]]);

/** Each section's heading and lines, as read. */
function read(): string[][] {
  return [...document.querySelectorAll("section")].map((section) =>
    [...section.querySelectorAll("h4, button, div > span.text-text-muted, li.text-text-muted, p")].map(
      (line) => line.textContent?.replace(/[  ]/g, " ") ?? "",
    ),
  );
}

describe("the blueprint's Summary", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("reads On start first, then each event by priority, the forced defaults, then the functions", () => {
    render(<Summary graphs={graphs} names={names} chosen={null} onChoose={() => {}} />);
    expect(read()).toEqual([
      [
        "Au lancement",
        "1. Aller dans à choisir étape 4",
        "2. Vider à la banque",
        "Si ça échoue :",
        "2.1. Attendre 5 min",
        "Revenir à 1.",
      ],
      ["Vie sous 40 % · priorité 2 · Interrompre, puis reprendre", "Ne fait rien."],
      ["Un combat commence · par défaut", "Combattre avec l'IA par défaut, puis reprendre.étape 6"],
      ["Le personnage est mort · par défaut", "Le lancement s'arrête, le personnage bloqué."],
      ["Une action échoue · par défaut", "Le personnage est bloqué, avec la raison."],
      ["Fonction Vider à la banque", "1. Terminer"],
    ]);
  });

  it("orders the events of every event graph together, On start first", () => {
    const [life, begin] = events.nodes;
    const pods = node("pods", "pods_above", {
      family: "event",
      pins: [pin("then", "out"), pin("threshold", "in", "percentage", "80")],
      event: { priority: 1, mode: "waitForAction" },
    });
    const split = [
      { ...events, id: "a", nodes: [life], wires: [] },
      { ...events, id: "b", nodes: [begin, pods], wires: [] },
    ];
    render(
      <Summary graphs={split as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    const headings = [...document.querySelectorAll("h4")].map((heading) => heading.textContent?.split(" · ")[0]);
    expect(headings.slice(0, 3)).toEqual(["Au lancement", "Pods au-dessus de 80 %", "Vie sous 40 %"]);
  });

  it("leaves out the default of a forced event the blueprint handles", () => {
    const handled = {
      ...events,
      nodes: [...events.nodes, node("fight", "fight_starts", { family: "event", pins: [pin("then", "out")] })],
    };
    render(
      <Summary
        graphs={[handled] as unknown as GraphView_Serialize[]}
        names={names}
        chosen={null}
        onChoose={() => {}}
      />,
    );
    const headings = [...document.querySelectorAll("h4")].map((heading) => heading.textContent);
    expect(headings).toContain("Un combat commence");
    expect(headings).not.toContain("Un combat commence · par défaut");
  });

  it("chooses a step's node, in its graph", () => {
    const onChoose = vi.fn();
    render(<Summary graphs={graphs} names={names} chosen={null} onChoose={onChoose} />);
    fireEvent.click(screen.getByRole("button", { name: /Terminer/ }));
    expect(onChoose).toHaveBeenCalledWith("bank", "finish");
  });

  it("marks the step of the node Details shows, in its graph alone", () => {
    // A node of the same id in each graph: only the chosen graph's is marked.
    const shared = [
      events,
      {
        ...bank,
        nodes: [...bank.nodes, node("go", "notify")],
        wires: [
          { from: ["entry", "then"], to: ["go", "run"] },
          { from: ["go", "done"], to: ["finish", "run"] },
        ],
      },
    ];
    render(
      <Summary
        graphs={shared as unknown as GraphView_Serialize[]}
        names={names}
        chosen={{ graph: "events", node: "go" }}
        onChoose={() => {}}
      />,
    );
    const marked = screen.getAllByRole("button").filter((step) => step.getAttribute("aria-current") === "true");
    expect(marked.map((step) => step.textContent)).toEqual(["1. Aller dans à choisir étape 4"]);
    // The function's own step 1 is the other "go".
    expect(screen.getByRole("button", { name: /^1\. Prévenir/ }).getAttribute("aria-current")).toBe("false");
  });
});
