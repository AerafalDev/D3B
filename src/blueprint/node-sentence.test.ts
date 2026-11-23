import i18next from "i18next";
import { beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { type GraphNode, graphSentences, nodeSentence } from "./node-sentence.ts";

const pin = (id: string, carries: string, fallback: string | null = null) =>
  ({ id, side: "in", carries, default: fallback }) as GraphNode["pins"][number];

const harvest = {
  id: "harvest",
  does: "kind",
  kind: "harvest_until",
  family: "action",
  fromStep: 5,
  pins: [pin("zone", "zone"), pin("what", "items", '"all"'), pin("pods", "percentage", "90")],
  inputs: { what: '"none"' },
  position: [0, 0],
  event: null,
} as GraphNode;

const names = new Map([
  ["zone", "Zone"],
  ["empty_at_bank", "Vider à la banque"],
]);
const plain = (parts: ReturnType<typeof nodeSentence>) =>
  parts.map((part) => ("text" in part ? part.text : `[${part.chip}]`).replace(/[  ]/g, " ")).join("");

describe("a node's sentence", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  it("fills each hole with the value typed, the pin wired, or the pin's default", () => {
    expect(plain(nodeSentence(harvest, new Map([["zone", "Zone"]]), names))).toBe(
      "Récolter [Rien] dans [Zone] jusqu'à [90 %] de pods",
    );
  });

  it("gives each value its type, for its dot", () => {
    const chips = nodeSentence(harvest, new Map(), names).flatMap((part) => ("chip" in part ? [part.valueType] : []));
    expect(chips).toEqual(["items", "zone", "percentage"]);
  });

  it("says a get, a set or a call by the variable's or the function's name", () => {
    const node = (does: object) => ({ ...harvest, ...does }) as GraphNode;
    expect(plain(nodeSentence(node({ does: "get", variable: "zone" }), new Map(), names))).toBe("Zone");
    expect(plain(nodeSentence(node({ does: "set", variable: "zone" }), new Map(), names))).toBe("Écrire Zone");
    expect(plain(nodeSentence(node({ does: "call", function: "empty_at_bank" }), new Map(), names))).toBe(
      "Vider à la banque",
    );
  });

  it("says Do N times of 1 as Do once, but not of 2 nor of a number wired", () => {
    const most = (inputs: object) =>
      ({ ...harvest, kind: "do_n_times", family: "flow", pins: [pin("times", "number", "1")], inputs }) as GraphNode;
    expect(plain(nodeSentence(most({}), new Map(), names))).toBe("Une seule fois");
    expect(plain(nodeSentence(most({ times: "2" }), new Map(), names))).toBe("Au plus [2] fois");
    expect(plain(nodeSentence(most({}), new Map([["times", "Tours"]]), names))).toBe("Au plus [Tours] fois");
  });

  it("says Wait until's longest wait when it has one", () => {
    const until = (inputs: object) =>
      ({
        ...harvest,
        kind: "wait_until",
        family: "flow",
        pins: [pin("condition", "bool"), pin("longest", "duration", "0")],
        inputs,
      }) as GraphNode;
    expect(plain(nodeSentence(until({}), new Map([["condition", "Vie ≥ 90"]]), names))).toBe("Attendre que [Vie ≥ 90]");
    expect(plain(nodeSentence(until({ longest: "300" }), new Map([["condition", "Vie ≥ 90"]]), names))).toBe(
      "Attendre que [Vie ≥ 90] (au plus [5 min])",
    );
    const wired = new Map([
      ["condition", "Vie ≥ 90"],
      ["longest", "Patience"],
    ]);
    expect(plain(nodeSentence(until({}), wired, names))).toBe("Attendre que [Vie ≥ 90] (au plus [Patience])");
  });

  it("says a kind this version does not know, with its id", () => {
    const unknown = { ...harvest, kind: "teleport", family: null, pins: [] } as GraphNode;
    expect(plain(nodeSentence(unknown, new Map(), names))).toBe("Nœud inconnu de cette version : teleport");
    // A family but no words for its kind: the same.
    const wordless = { ...harvest, kind: "teleport", pins: [] } as GraphNode;
    expect(plain(nodeSentence(wordless, new Map(), names))).toBe("Nœud inconnu de cette version : teleport");
  });

  it("says what a wire brings: a variable's name, a query's own sentence, else its source pin's label", () => {
    const job = {
      ...harvest,
      id: "job",
      does: "get",
      variable: "job",
      family: "variable",
      fromStep: null,
      pins: [],
      inputs: {},
    };
    const resources = {
      ...harvest,
      id: "resources",
      kind: "job_resources",
      family: "query",
      fromStep: null,
      pins: [pin("job", "job")],
      inputs: {},
    };
    const graph = {
      id: "events",
      name: "Event graph",
      function: false,
      nodes: [harvest, job, resources] as GraphNode[],
      wires: [
        { from: ["resources", "value"], to: ["harvest", "what"] },
        { from: ["job", "value"], to: ["resources", "job"] },
      ],
    } as never;
    const sentences = graphSentences(graph, new Map([["job", "Métier"]]));
    expect(plain(sentences.get("harvest") ?? [])).toBe(
      "Récolter [Ressources de Métier] dans [à choisir] jusqu'à [90 %] de pods",
    );
  });

  it("says a source pin no translation names by its id, and a loop of queries by the pin's label", () => {
    const add = (id: string) =>
      ({
        ...harvest,
        id,
        kind: "add",
        family: "value",
        fromStep: null,
        pins: [pin("a", "number"), pin("b", "number")],
        inputs: {},
      }) as GraphNode;
    const entry = {
      ...harvest,
      id: "entry",
      kind: "entry",
      family: "function",
      fromStep: null,
      pins: [],
      inputs: {},
    } as GraphNode;
    const graph = {
      id: "f",
      name: "f",
      function: true,
      nodes: [harvest, entry, add("one"), add("two")],
      wires: [
        { from: ["entry", "where"], to: ["harvest", "zone"] },
        { from: ["one", "value"], to: ["two", "a"] },
        { from: ["two", "value"], to: ["one", "a"] },
      ],
    } as never;
    const sentences = graphSentences(graph, new Map());
    expect(plain(sentences.get("harvest") ?? [])).toContain("dans [where]");
    expect(plain(sentences.get("one") ?? [])).toBe("[(Valeur + à choisir)] + [à choisir]");
  });

  const value = (id: string, kind: string, pins: GraphNode["pins"], more: object = {}) =>
    ({ ...harvest, id, kind, family: "value", fromStep: null, pins, inputs: {}, ...more }) as GraphNode;
  const get = (id: string) =>
    ({
      ...harvest,
      id,
      does: "get",
      variable: id,
      family: "variable",
      fromStep: null,
      pins: [],
      inputs: {},
    }) as GraphNode;
  const graphOf = (nodes: GraphNode[], wires: [string, string, string][]) =>
    ({
      id: "events",
      name: "Event graph",
      function: false,
      nodes,
      wires: wires.map(([from, to, at]) => ({ from: [from, "value"], to: [to, at] })),
    }) as never;

  it("reads a value in place, in parentheses inside another value, without them in a flow node's", () => {
    const graph = graphOf(
      [
        get("harvests"),
        value("add", "add", [pin("a", "number"), pin("b", "number")], { inputs: { b: "5" } }),
        value("times", "multiply", [pin("a", "number"), pin("b", "number")], { inputs: { b: "2" } }),
        value("pods", "pods_percent", [], { family: "query" }),
        value("less", "less", [pin("a", "percentage"), pin("b", "number")], { inputs: { b: "80" } }),
        value("while", "while", [pin("condition", "bool")], { family: "flow" }),
        value("not", "not", [pin("value", "bool")]),
        value("count", "length", [pin("list", "number")]),
        value("more", "add", [pin("a", "number"), pin("b", "number")], { inputs: { b: "1" } }),
      ],
      [
        ["harvests", "add", "a"],
        ["add", "times", "a"],
        ["pods", "less", "a"],
        ["less", "while", "condition"],
        ["pods", "not", "value"],
        ["harvests", "count", "list"],
        ["count", "more", "a"],
      ],
    );
    const sentences = graphSentences(graph, new Map([["harvests", "Récoltes"]]));
    expect(plain(sentences.get("times") ?? [])).toBe("[(Récoltes + 5)] × [2]");
    expect(plain(sentences.get("while") ?? [])).toBe("Tant que [Pods % < 80]");
    expect(plain(sentences.get("not") ?? []), "a value of no value needs none").toBe("NON [Pods %]");
    expect(plain(sentences.get("more") ?? []), "nor one of one value").toBe("[Nombre d'éléments de Récoltes] + [1]");
  });

  it("joins AND's and a List's inputs by their word, each by what feeds it", () => {
    const adds = (pin: string, least: number) => ({ adds: { pin, side: "in", least } });
    const and = value("and", "and", [pin("in_1", "bool"), pin("in_2", "bool"), pin("in_3", "bool")], {
      ...adds("in", 2),
      inputs: { in_2: "true" },
    });
    const list = value("list", "make_list", [pin("in_1", "number"), pin("in_2", "number")], {
      ...adds("in", 1),
      inputs: { in_1: "30", in_2: "60" },
    });
    const sentences = graphSentences(graphOf([get("full"), and, list], [["full", "and", "in_1"]]), new Map());
    expect(plain(sentences.get("and") ?? [])).toBe("[full] ET [Oui] ET [à choisir]");
    expect(plain(sentences.get("list") ?? [])).toBe("Liste de [30], [60]");
  });

  it("reads a Text as its template filled, a brace naming no input kept as written", () => {
    const text = (template: string | null) =>
      value("said", "text", [pin("n", "number"), pin("temps", "duration")], {
        takesText: true,
        options: { text: template },
        inputs: { temps: "90" },
      });
    const graph = (template: string | null) => graphOf([get("harvests"), text(template)], [["harvests", "said", "n"]]);
    const read = (template: string | null) =>
      plain(graphSentences(graph(template), new Map([["harvests", "Récoltes"]])).get("said") ?? []);
    expect(read("Récolté {n} en {temps} {autre} {")).toBe("Récolté [Récoltes] en [1 min 30] {autre} {");
    const inText = graphOf(
      [value("sum", "add", [pin("a", "number"), pin("b", "number")], { inputs: { a: "1", b: "2" } }), text("= {n}")],
      [["sum", "said", "n"]],
    );
    expect(plain(graphSentences(inText, new Map()).get("said") ?? []), "as the run says it").toBe("= [1 + 2]");
    expect(read(null), "a Text with no template says its kind").toBe("Texte");
  });
});
