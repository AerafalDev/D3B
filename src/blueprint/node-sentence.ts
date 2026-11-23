import i18next from "i18next";

import type { GraphView_Serialize, NodeView_Serialize } from "@/bindings.ts";
import { nodeWord } from "@/i18n/node-words.ts";
import { valueText } from "@/library/value-text.ts";
import { pinLabel } from "./pin-label.ts";

/** A part of a node's sentence: words, or a value in its chip, with its type for its dot. */
export type SentencePart = { text: string } | { chip: string; valueType: string };

/** A node of a graph, as the IPC gives it (`ipc::graph`). */
export type GraphNode = NodeView_Serialize;

/**
 * A node's sentence (`kinds.<kind>.sentence` of the nodes' words), its holes filled with what feeds each: what a wire brings, said by
 * `wired` (a variable's name, or its source pin's label), the value typed on the pin, or the pin's default; a get, a set or a call says its variable or
 * function by `names`. A kind this version does not know says so with its id.
 */
export function nodeSentence(
  node: GraphNode,
  wired: ReadonlyMap<string, string>,
  names: ReadonlyMap<string, string>,
): SentencePart[] {
  const language = i18next.language;
  switch (node.does) {
    case "get":
      return [{ text: names.get(node.variable) ?? node.variable }];
    case "set":
      return [{ text: i18next.t("blueprint.set", { name: names.get(node.variable) ?? node.variable }) }];
    case "call":
      return [{ text: names.get(node.function) ?? node.function }];
    case "kind":
      break;
  }
  // The sentence as written, its holes kept: the player's language, else English. Do N times of 1 is Do once (the
  // owner, 2026-11-17): "Une seule fois".
  const once = node.kind === "do_n_times" && !wired.has("times") && (node.inputs["times"] ?? "1") === "1";
  if (once && node.family !== null) {
    return [{ text: i18next.t("blueprint.doOnce") }];
  }
  const raw = nodeWord(`kinds.${node.kind}.sentence`);
  if (node.family === null || raw === null) {
    return [{ text: i18next.t("blueprint.unknownKind", { kind: node.kind }) }];
  }
  const chipOf = (id: string): SentencePart => {
    const pin = node.pins.find((one) => one.id === id && one.side === "in");
    const valueType = pin?.carries ?? "text";
    const typed = node.inputs[id];
    const chip = wired.get(id) ?? valueText(valueType, typed ?? pin?.default ?? null, language);
    return { chip, valueType };
  };
  const template = node.options?.text ?? "";
  if (node.takesText === true && template !== "") {
    return filled(template, node, chipOf);
  }
  const parts: SentencePart[] = [];
  for (const [index, piece] of raw.split(/\{\{(\w+)\}\}/).entries()) {
    if (index % 2 === 0) {
      if (piece !== "") {
        parts.push({ text: piece });
      }
      continue;
    }
    const numbered = node.adds?.side === "in" && node.adds.pin === piece;
    if (!numbered) {
      parts.push(chipOf(piece));
      continue;
    }
    // AND's, OR's and a List's inputs one after another, joined by the kind's word: "A ET B ET C".
    const between = nodeWord(`kinds.${node.kind}.between`) ?? ", ";
    const ins = node.pins.filter((one) => one.side === "in" && new RegExp(`^${piece}_[1-9][0-9]*$`).test(one.id));
    for (const [at, pin] of ins.entries()) {
      if (at > 0) {
        parts.push({ text: between });
      }
      parts.push(chipOf(pin.id));
    }
  }
  // Wait until's longest wait, 0 by default for none, said when it has one (the owner's mock-up of 2026-11-17).
  const longest = node.inputs["longest"] ?? node.pins.find((one) => one.id === "longest")?.default ?? "0";
  if (node.kind === "wait_until" && (wired.has("longest") || longest !== "0")) {
    const chip = wired.get("longest") ?? valueText("duration", longest, language);
    const words = String(i18next.t("blueprint.atMostOf", { longest: "{{longest}}" }));
    for (const [index, piece] of words.split(/\{\{longest\}\}/).entries()) {
      if (index > 0) {
        parts.push({ chip, valueType: "duration" });
      }
      if (piece !== "") {
        parts.push({ text: piece });
      }
    }
  }
  return parts;
}

/**
 * A Text's template filled as the run fills it (`values/text.rs`): a `{hole}` naming one of its inputs is that input's
 * chip, any other brace stays as written.
 */
function filled(template: string, node: GraphNode, chipOf: (id: string) => SentencePart): SentencePart[] {
  const parts: SentencePart[] = [];
  let words = "";
  let rest = template;
  for (let at = rest.indexOf("{"); at !== -1; at = rest.indexOf("{")) {
    words += rest.slice(0, at);
    const end = rest.indexOf("}", at);
    const hole = end === -1 ? null : rest.slice(at + 1, end);
    if (hole !== null && node.pins.some((pin) => pin.id === hole && pin.side === "in")) {
      if (words !== "") {
        parts.push({ text: words });
      }
      words = "";
      parts.push(chipOf(hole));
      rest = rest.slice(end + 1);
    } else {
      words += "{";
      rest = rest.slice(at + 1);
    }
  }
  words += rest;
  if (words !== "") {
    parts.push({ text: words });
  }
  return parts;
}

/** A sentence read as plain words, its chips' values among them. */
function plain(parts: SentencePart[]): string {
  return parts.map((part) => ("text" in part ? part.text : part.chip)).join("");
}

/**
 * Every node's sentence in `graph`, each wired input saying what it brings: a variable's name, a query's or a value's
 * own sentence read in place ("Ressources de Métier", "Pods % < 80"), else its source pin's label ("Récoltes"). A
 * loop of queries, which no graph should hold, says the pin's label.
 */
export function graphSentences(
  graph: GraphView_Serialize,
  names: ReadonlyMap<string, string>,
): Map<string, SentencePart[]> {
  const byId = new Map(graph.nodes.map((node) => [node.id, node]));
  const sentences = new Map<string, SentencePart[]>();
  const reading = new Set<string>();
  const sentence = (node: GraphNode): SentencePart[] => {
    const known = sentences.get(node.id);
    if (known !== undefined) {
      return known;
    }
    reading.add(node.id);
    const wired = new Map<string, string>();
    for (const wire of graph.wires.filter((one) => one.to[0] === node.id)) {
      const source = byId.get(wire.from[0]);
      const label = pinLabel(wire.from[1], source);
      if (source?.does === "get") {
        wired.set(wire.to[1], names.get(source.variable) ?? source.variable);
      } else if ((source?.family === "query" || source?.family === "value") && !reading.has(source.id)) {
        const nested = sentence(source);
        // A value of two values or more read inside another value is put in parentheses, so "(Récoltes + 5) × 2" reads
        // as it computes (settled alone); a flow node's sentence needs none, "Tant que Pods % < 80", nor a Text's, which
        // reads as the run fills it.
        const inner = node.family === "value" && node.takesText !== true;
        const grouped = inner && nested.filter((part) => "chip" in part).length > 1;
        wired.set(wire.to[1], grouped ? `(${plain(nested)})` : plain(nested));
      } else {
        wired.set(wire.to[1], label);
      }
    }
    reading.delete(node.id);
    const parts = nodeSentence(node, wired, names);
    sentences.set(node.id, parts);
    return parts;
  };
  for (const node of graph.nodes) {
    sentence(node);
  }
  return sentences;
}
