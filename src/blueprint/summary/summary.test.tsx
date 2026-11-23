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
      pins: [pin("run", "in"), pin("then", "out"), pin("duration", "in", "duration", "300")],
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

  it("reads a Sequence's outputs as sub-steps and a Switch's on one line, each to its step", () => {
    const exec = (id: string) => pin(id, "out");
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("seq", "sequence", {
          family: "flow",
          pins: [pin("run", "in"), exec("then_1"), exec("then_2")],
          adds: { pin: "then", side: "out", least: 2 },
        }),
        node("sw", "switch", {
          family: "flow",
          pins: [pin("run", "in"), exec("case_1"), exec("case_2"), exec("otherwise")],
          options: { valueType: "number", cases: ["24", "28"] },
          takesCases: true,
        }),
        node("a", "notify"),
        node("end", "finish", { family: "flow", pins: [pin("run", "in")] }),
      ],
      wires: [
        { from: ["start", "then"], to: ["seq", "run"] },
        { from: ["seq", "then_1"], to: ["a", "run"] },
        { from: ["seq", "then_2"], to: ["sw", "run"] },
        { from: ["sw", "case_1"], to: ["end", "run"] },
        { from: ["sw", "otherwise"], to: ["a", "run"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual([
      "Au lancement",
      "1. Séquence",
      "1.1. Prévenir : à choisir",
      "2. Selon à choisir",
      "24 → 2.1 · 28 → rien · Sinon → 1.1",
      "2.1. Terminer",
    ]);
  });

  it("titles a function's named exit by its name, an id of If's own too", () => {
    const calling = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("bank", "call", {
          does: "call",
          function: "bank",
          family: "call",
          pins: [...flow, pin("true", "out")],
          exits: [{ id: "true", name: "Plein" }],
        }),
        node("go", "notify"),
      ],
      wires: [
        { from: ["start", "then"], to: ["bank", "run"] },
        { from: ["bank", "true"], to: ["go", "run"] },
      ],
    };
    render(
      <Summary
        graphs={[calling] as unknown as GraphView_Serialize[]}
        names={names}
        chosen={null}
        onChoose={() => {}}
      />,
    );
    expect(read()[0]).toEqual(["Au lancement", "1. Vider à la banque", "Si Plein :", "1.1. Prévenir : à choisir"]);
  });

  it("names a Sequence's output that leads straight to a step said, and numbers the next when one is unwired", () => {
    const exec = (id: string) => pin(id, "out");
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("seq", "sequence", {
          family: "flow",
          pins: [pin("run", "in"), exec("then_1"), exec("then_2"), exec("then_3"), exec("then_4")],
          adds: { pin: "then", side: "out", least: 2 },
        }),
        node("a", "notify"),
      ],
      wires: [
        { from: ["start", "then"], to: ["seq", "run"] },
        { from: ["seq", "then_2"], to: ["a", "run"] },
        { from: ["seq", "then_3"], to: ["seq", "run"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual(["Au lancement", "1. Séquence", "1.1. Prévenir : à choisir", "Alors 3 : aller à 1."]);
  });

  it("reads a loop's body under its title, its Completed as what follows it, and a jump out of it as such", () => {
    const loop = (id: string, kind: string) =>
      node(id, kind, {
        family: "flow",
        pins: [pin("run", "in"), pin("body", "out"), pin("completed", "out"), pin("times", "in", "number", "3")],
      });
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("a", "notify"),
        loop("rep", "repeat"),
        loop("each", "for_each"),
        node("b", "notify"),
      ],
      wires: [
        { from: ["start", "then"], to: ["a", "run"] },
        { from: ["a", "done"], to: ["rep", "run"] },
        { from: ["rep", "body"], to: ["each", "run"] },
        { from: ["each", "body"], to: ["b", "run"] },
        { from: ["each", "completed"], to: ["a", "run"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual([
      "Au lancement",
      "1. Prévenir : à choisir",
      "2. Répéter 3 fois",
      "À chaque tour :",
      "2.1. Pour chaque à choisir",
      "Pour chaque élément :",
      "2.1.1. Prévenir : à choisir",
      "Puis (terminé) : aller à 1.",
    ]);
    cleanup();
    // A call's exit named `completed` is no loop's: going back by its Done reads as any jump back.
    const calling = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("bank", "call", {
          does: "call",
          function: "bank",
          family: "call",
          pins: [...flow, pin("completed", "out")],
          exits: [{ id: "completed", name: "Fini" }],
        }),
      ],
      wires: [
        { from: ["start", "then"], to: ["bank", "run"] },
        { from: ["bank", "done"], to: ["bank", "run"] },
      ],
    };
    render(
      <Summary
        graphs={[calling] as unknown as GraphView_Serialize[]}
        names={names}
        chosen={null}
        onChoose={() => {}}
      />,
    );
    expect(read()[0]).toEqual(["Au lancement", "1. Vider à la banque", "Revenir à 1."]);
  });

  it("reads Do once by its name, a wire into its Reset as a re-arm, and Alternate's outputs on one line", () => {
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("once", "do_n_times", {
          family: "flow",
          pins: [pin("run", "in"), pin("reset", "in"), pin("times", "in", "number", "1"), pin("then", "out")],
        }),
        node("alt", "alternate", {
          family: "flow",
          pins: [pin("run", "in"), pin("first", "out"), pin("second", "out")],
        }),
        node("a", "notify"),
        node("b", "notify"),
      ],
      wires: [
        { from: ["start", "then"], to: ["once", "run"] },
        { from: ["once", "then"], to: ["alt", "run"] },
        { from: ["alt", "first"], to: ["a", "run"] },
        { from: ["alt", "second"], to: ["b", "run"] },
        { from: ["b", "done"], to: ["once", "reset"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual([
      "Au lancement",
      "1. Une seule fois",
      "2. Alterner",
      "D'abord → 2.1 · Ensuite → 2.2",
      "2.1. Prévenir : à choisir",
      "2.2. Prévenir : à choisir",
      "Réarmer 1.",
    ]);
  });

  it("says a re-arm as a branch's target, and one of a node numbered nowhere by its sentence", () => {
    const once = (id: string, times?: string) =>
      node(id, "do_n_times", {
        family: "flow",
        pins: [pin("run", "in"), pin("reset", "in"), pin("times", "in", "number", "1"), pin("then", "out")],
        inputs: times === undefined ? {} : { times },
      });
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        once("once"),
        once("other", "2"),
        node("alt", "alternate", {
          family: "flow",
          pins: [pin("run", "in"), pin("first", "out"), pin("second", "out")],
        }),
        node("seq", "sequence", {
          family: "flow",
          pins: [pin("run", "in"), pin("then_1", "out"), pin("then_2", "out")],
          adds: { pin: "then", side: "out", least: 2 },
        }),
      ],
      wires: [
        { from: ["start", "then"], to: ["once", "run"] },
        { from: ["once", "then"], to: ["alt", "run"] },
        { from: ["alt", "second"], to: ["once", "reset"] },
        { from: ["alt", "first"], to: ["seq", "run"] },
        { from: ["seq", "then_1"], to: ["once", "reset"] },
        { from: ["seq", "then_2"], to: ["other", "reset"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual([
      "Au lancement",
      "1. Une seule fois",
      "2. Alterner",
      "D'abord → 2.1 · Ensuite → réarmer 1",
      "2.1. Séquence",
      "Alors 1 : réarmer 1.",
      "Réarmer « Au plus 2 fois ».",
    ]);
  });

  it("titles Wait until's Too long", () => {
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("until", "wait_until", {
          family: "flow",
          pins: [pin("run", "in"), pin("condition", "in", "bool"), pin("then", "out"), pin("too_long", "out")],
          inputs: { condition: "true" },
        }),
        node("a", "notify"),
      ],
      wires: [
        { from: ["start", "then"], to: ["until", "run"] },
        { from: ["until", "too_long"], to: ["a", "run"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual(["Au lancement", "1. Attendre que Oui", "Trop long :", "1.1. Prévenir : à choisir"]);
  });

  it("reads the values in place in their steps, never as steps of their own", () => {
    const value = (id: string, kind: string, pins: object[], inputs: object = {}) =>
      node(id, kind, { family: "value", pins: [...pins, pin("value", "out", "number")], inputs });
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("until", "wait_until", {
          family: "flow",
          pins: [pin("run", "in"), pin("condition", "in", "bool"), pin("then", "out"), pin("too_long", "out")],
        }),
        node("pods", "pods_percent", { family: "query", pins: [pin("value", "out", "percentage")] }),
        value("less", "less", [pin("a", "in", "percentage"), pin("b", "in", "number")], { b: "80" }),
        node("said", "text", {
          family: "value",
          takesText: true,
          options: { text: "Pods à {p}" },
          pins: [pin("p", "in", "percentage"), pin("value", "out", "text")],
        }),
        node("tell", "notify", { pins: [...flow, pin("text", "in", "text")] }),
      ],
      wires: [
        { from: ["start", "then"], to: ["until", "run"] },
        { from: ["pods", "value"], to: ["less", "a"] },
        { from: ["less", "value"], to: ["until", "condition"] },
        { from: ["until", "then"], to: ["tell", "run"] },
        { from: ["pods", "value"], to: ["said", "p"] },
        { from: ["said", "value"], to: ["tell", "text"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual(["Au lancement", "1. Attendre que Pods % < 80", "2. Prévenir : Pods à Pods %"]);
  });

  it("titles Retry's try and its Failed after every try", () => {
    const flows = {
      ...events,
      nodes: [
        node("start", "on_start", { family: "event", pins: [pin("then", "out")] }),
        node("retry", "retry", {
          family: "flow",
          pins: [
            pin("run", "in"),
            pin("times", "in", "number", "3"),
            pin("try", "out"),
            pin("then", "out"),
            pin("failed", "out"),
          ],
        }),
        node("go", "go_to_zone"),
        node("say", "notify"),
      ],
      wires: [
        { from: ["start", "then"], to: ["retry", "run"] },
        { from: ["retry", "try"], to: ["go", "run"] },
        { from: ["retry", "failed"], to: ["say", "run"] },
      ],
    };
    render(
      <Summary graphs={[flows] as unknown as GraphView_Serialize[]} names={names} chosen={null} onChoose={() => {}} />,
    );
    expect(read()[0]).toEqual([
      "Au lancement",
      "1. Essayer jusqu'à 3 fois",
      "Essai :",
      "1.1. Aller dans à choisir",
      "Si tous les essais échouent :",
      "1.2. Prévenir : à choisir",
    ]);
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
