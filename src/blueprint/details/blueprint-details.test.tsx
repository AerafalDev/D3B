import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import type { GraphView_Serialize, ProblemView } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import type { GraphNode } from "../node-sentence.ts";
import { BlueprintDetails, type Selection } from "./blueprint-details.tsx";

const pin = (id: string, side: "in" | "out", carries: string, fallback: string | null = null) =>
  ({ id, side, carries, default: fallback }) as GraphNode["pins"][number];

const node = (id: string, more: object) =>
  ({
    id,
    does: "kind",
    family: "action",
    fromStep: null,
    pins: [],
    inputs: {},
    position: [0, 0],
    event: null,
    ...more,
  }) as unknown as GraphNode;

const graph = {
  id: "events",
  name: "Event graph",
  function: false,
  inputs: [],
  outputs: [],
  nodes: [
    node("harvest", {
      kind: "harvest_until",
      fromStep: 5,
      pins: [
        pin("run", "in", "exec"),
        pin("failed", "out", "exec"),
        pin("zone", "in", "zone"),
        pin("pods", "in", "percentage", "90"),
      ],
    }),
    node("zone", { does: "get", variable: "zone", family: "variable", pins: [pin("value", "out", "zone")] }),
    node("life", {
      kind: "life_below",
      family: "event",
      pins: [
        pin("then", "out", "exec"),
        pin("threshold", "in", "percentage", "40"),
        pin("rearm", "in", "percentage", "60"),
      ],
      event: { priority: 2, mode: "restart" },
    }),
  ],
  wires: [{ from: ["zone", "value"], to: ["harvest", "zone"] }],
} as unknown as GraphView_Serialize;

const blueprint: BlueprintSummary = {
  id: "automatic-harvest",
  name: "Automatic harvest",
  description: "",
  given: true,
  version: 1,
  basedOn: null,
  problems: 1,
  variables: [
    {
      id: "pods",
      name: "Bank at",
      valueType: "percentage",
      default: "90",
      bounds: [10, 100],
      perCharacter: true,
      memory: false,
      help: "",
    },
    {
      id: "zone",
      name: "Zone",
      valueType: "zone",
      default: null,
      bounds: null,
      perCharacter: true,
      memory: false,
      help: "",
    },
  ],
};

const problem: ProblemView = {
  kind: "inputWithoutValue",
  graph: "events",
  node: "harvest",
  pin: "zone",
  subject: null,
};
const names = new Map([["zone", "Zone"]]);

let graphs: GraphView_Serialize[] = [graph];

function details(selection: Selection, problems: ProblemView[] = []) {
  return render(
    <BlueprintDetails
      blueprint={blueprint}
      graphs={graphs}
      graph={graph}
      problems={problems}
      selection={selection}
      names={names}
    />,
  );
}

describe("the editor's Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("asks to choose something, and lists the blueprint's problems", () => {
    const { container } = details(null, [problem]);
    expect(container.textContent).toContain("Choisis un nœud ou une variable.");
    // The node first, so the player finds it; the pin by its label.
    expect(screen.getByRole("listitem").textContent).toBe(
      "Récolter à choisir dans Zone jusqu'à 90 % de pods : L'entrée Zone n'a pas de valeur.",
    );
  });

  it("says a node's sentence, its description, its step, its inputs and what an unlinked Failed does", () => {
    const { container } = details({ kind: "node", id: "harvest" }, [problem]);
    const text = container.textContent?.replace(/[  ]/g, " ") ?? "";
    expect(text).toContain("Récolter à choisir dans Zone jusqu'à 90 % de pods");
    expect(text).toContain("Récolte dans la zone les ressources choisies");
    expect(text).toContain("Le bot le fera à partir de l'étape 5.");
    const inputs = [...container.querySelectorAll("dt")].map(
      (term) => `${term.textContent}: ${term.nextSibling?.textContent}`,
    );
    expect(inputs.map((line) => line.replace(/[  ]/g, " "))).toEqual(["Zone: relié", "Pods: 90 %"]);
    expect(text).toContain("Non reliée, la sortie Échec bloque le personnage, avec la raison.");
    expect(text).toContain("L'entrée Zone n'a pas de valeur.");
    expect(screen.queryByRole("textbox"), "D3B's, without onValue, edits nothing").toBeNull();
  });

  it("says an event's priority, its re-arming and the mode it has, among the four", () => {
    const { container } = details({ kind: "node", id: "life" });
    const text = container.textContent?.replace(/[  ]/g, " ") ?? "";
    expect(text).toContain("2 · 1 passe avant 2");
    // Its re-arming is among its inputs, said once.
    expect(text.match(/Réarmement/g)).toHaveLength(1);
    const modes = screen.getAllByRole("listitem");
    expect(modes).toHaveLength(4);
    expect(
      modes.filter((mode) => mode.getAttribute("aria-current") === "true").map((mode) => mode.textContent),
    ).toEqual(["Interrompre, puis tout recommencer"]);
  });

  it("says a variable's type, default, bounds, who sets it, how long it lasts and the nodes that use it", () => {
    const { container } = details({ kind: "variable", id: "pods" });
    const text = container.textContent?.replace(/[  ]/g, " ") ?? "";
    for (const said of [
      "Banque à",
      "Pourcentage",
      "90 %",
      "de 10 à 100",
      "Oui : chaque personnage",
      "Le temps d'un lancement",
      "0 nœud",
    ]) {
      expect(text).toContain(said);
    }
    // The nodes of every graph count, a function's among them.
    graphs = [graph, { ...graph, id: "bank", function: true, nodes: graph.nodes.filter((one) => one.id === "zone") }];
    const zone = details({ kind: "variable", id: "zone" }).container.textContent ?? "";
    expect(zone).toContain("2 nœuds");
    graphs = [graph];
  });

  it("names a problem's subject by its name, else by its id", async () => {
    const { problemText } = await import("./problem-text.ts");
    const missing = { kind: "noSuchVariable", graph: "events", node: "n", pin: null, subject: "zone" } as const;
    expect(problemText(missing, names)).toBe("Ce nœud lit ou écrit Zone, que ce blueprint n'a pas.");
    expect(problemText({ ...missing, subject: "bank" }, names)).toBe(
      "Ce nœud lit ou écrit bank, que ce blueprint n'a pas.",
    );
  });

  it("says the main flow takes no priority", () => {
    const start = {
      ...graph,
      nodes: [{ ...graph.nodes[2], id: "start", kind: "on_start", pins: [], event: null }],
    } as never;
    const { container } = render(
      <BlueprintDetails
        blueprint={blueprint}
        graphs={[start]}
        graph={start}
        problems={[]}
        selection={{ kind: "node", id: "start" }}
        names={names}
      />,
    );
    expect(container.textContent).toContain("Le flux principal");
    expect(screen.queryByRole("listitem")).toBeNull();
  });

  it("types a value on an untyped comparison as the type chosen beside it, read as a number with none", async () => {
    const ge = node("ge", {
      kind: "greater_or_equal",
      family: "query",
      pins: [{ ...pin("b", "in", "number"), wildcard: true }, pin("value", "out", "bool")],
      types: ["number", "percentage", "duration", "time"],
    });
    const mine = { ...graph, nodes: [ge] } as GraphView_Serialize;
    const onValue = vi.fn(() => Promise.resolve(true));
    const details = (
      <BlueprintDetails
        blueprint={{ ...blueprint, given: false }}
        graphs={[mine]}
        graph={mine}
        problems={[]}
        selection={{ kind: "node", id: "ge" }}
        names={names}
        onValue={onValue}
      />
    );
    render(details);
    const number = screen.getByRole("textbox", { name: "B" });
    fireEvent.change(number, { target: { value: "90" } });
    fireEvent.keyDown(number, { key: "Enter" });
    expect(onValue).toHaveBeenLastCalledWith("ge", "b", "90", undefined);
    fireEvent.click(screen.getByRole("combobox", { name: "Type de B" }));
    fireEvent.click(await screen.findByRole("option", { name: "Heure" }));
    const time = screen.getByRole("textbox", { name: "B" });
    expect(time.getAttribute("aria-invalid")).toBe("false");
    for (const wrong of ["25:00", "9:00"]) {
      fireEvent.change(time, { target: { value: wrong } });
      expect(time.getAttribute("aria-invalid")).toBe("true");
      fireEvent.keyDown(time, { key: "Enter" });
      expect((time as HTMLInputElement).value).toBe("");
    }
    expect(onValue).toHaveBeenCalledTimes(1);
    fireEvent.change(time, { target: { value: "23:00" } });
    fireEvent.keyDown(time, { key: "Enter" });
    expect(onValue).toHaveBeenLastCalledWith("ge", "b", '"23:00"', "time");
    cleanup();
    // Typed, it offers no type to choose.
    const typed = { ...ge, options: { valueType: "time" }, pins: [{ ...pin("b", "in", "time"), wildcard: true }] };
    const shown = { ...mine, nodes: [typed] } as GraphView_Serialize;
    render(
      <BlueprintDetails
        blueprint={{ ...blueprint, given: false }}
        graphs={[shown]}
        graph={shown}
        problems={[]}
        selection={{ kind: "node", id: "ge" }}
        names={names}
        onValue={onValue}
      />,
    );
    expect(screen.queryByRole("combobox", { name: "Type de B" })).toBeNull();
    cleanup();
    // Another wildcard holding a value reads it as a number already: no type to choose on this one.
    const read = { ...ge, inputs: { a: "90" }, pins: [{ ...pin("a", "in", "number"), wildcard: true }, ...ge.pins] };
    const reading = { ...mine, nodes: [read] } as GraphView_Serialize;
    render(
      <BlueprintDetails
        blueprint={{ ...blueprint, given: false }}
        graphs={[reading]}
        graph={reading}
        problems={[]}
        selection={{ kind: "node", id: "ge" }}
        names={names}
        onValue={onValue}
      />,
    );
    expect(screen.queryByRole("combobox", { name: "Type de B" })).toBeNull();
  });

  it("offers any type on a wildcard reading none, and always says the type chosen", async () => {
    const any = node("eq", {
      kind: "test_equal",
      family: "query",
      pins: [{ ...pin("b", "in", "wildcard"), wildcard: true }],
    });
    const mine = { ...graph, nodes: [any] } as GraphView_Serialize;
    const onValue = vi.fn(() => Promise.resolve(true));
    render(
      <BlueprintDetails
        blueprint={{ ...blueprint, given: false }}
        graphs={[mine]}
        graph={mine}
        problems={[]}
        selection={{ kind: "node", id: "eq" }}
        names={names}
        onValue={onValue}
      />,
    );
    expect(screen.getByRole("combobox", { name: "Type de B" }).textContent).toContain("Oui / non");
    fireEvent.click(screen.getByRole("switch", { name: "B" }));
    expect(onValue).toHaveBeenLastCalledWith("eq", "b", "true", "bool");
  });

  it("edits the values typed on a node's inputs of mine, a wired one said, a default put back", () => {
    const notify = node("notify", {
      kind: "notify",
      pins: [pin("run", "in", "exec"), pin("text", "in", "text")],
      inputs: { text: '"Fini"' },
    });
    const harvest = {
      ...graph.nodes[0],
      pins: [...(graph.nodes[0]?.pins ?? []), pin("what", "in", "items", '"all"')],
      inputs: { pods: "80", what: '"all"' },
    } as GraphNode;
    const set = node("set", {
      does: "set",
      variable: "pods",
      family: "variable",
      pins: [pin("value", "in", "percentage"), pin("value", "out", "percentage")],
    });
    const mine = { ...graph, nodes: [harvest, notify, set] } as GraphView_Serialize;
    const onValue = vi.fn(() => Promise.resolve(true));
    const shown = (id: string) =>
      render(
        <BlueprintDetails
          blueprint={{ ...blueprint, given: false }}
          graphs={[mine]}
          graph={mine}
          problems={[]}
          selection={{ kind: "node", id }}
          names={names}
          onValue={onValue}
        />,
      );
    const { unmount } = shown("notify");
    const text = screen.getByRole("textbox", { name: "Texte" });
    fireEvent.change(text, { target: { value: "Objectif atteint" } });
    fireEvent.keyDown(text, { key: "Enter" });
    expect(onValue).toHaveBeenLastCalledWith("notify", "text", JSON.stringify("Objectif atteint"), undefined);
    // Cleared, it is none, so the node keeps its problem.
    fireEvent.change(text, { target: { value: "" } });
    fireEvent.keyDown(text, { key: "Enter" });
    expect(onValue).toHaveBeenLastCalledWith("notify", "text", null, undefined);
    unmount();

    const { container, unmount: away } = shown("harvest");
    expect(container.textContent).toContain("relié");
    const pods = screen.getByRole("textbox", { name: "Pods" }) as HTMLInputElement;
    expect(pods.value).toBe("80");
    // A value typed at its default, as D3B's files hold some, offers nothing to put back.
    expect(screen.getAllByRole("button", { name: /Revenir/ })).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "Revenir à la valeur par défaut (90 %)" }));
    expect(onValue).toHaveBeenLastCalledWith("harvest", "pods", null, undefined);
    fireEvent.change(pods, { target: { value: "101" } });
    fireEvent.blur(pods);
    expect(onValue).toHaveBeenCalledTimes(3);
    away();

    // A Set keeps its variable's bounds: 10 to 100.
    shown("set");
    const value = screen.getByRole("textbox", { name: "Valeur" });
    fireEvent.change(value, { target: { value: "5" } });
    fireEvent.blur(value);
    expect(onValue).toHaveBeenCalledTimes(3);
    fireEvent.change(value, { target: { value: "50" } });
    fireEvent.blur(value);
    expect(onValue).toHaveBeenLastCalledWith("set", "value", "50", undefined);
  });
});
