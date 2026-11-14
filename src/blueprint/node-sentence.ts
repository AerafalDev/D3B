import i18next from "i18next";

import type { GraphView_Serialize, NodeView_Serialize } from "@/bindings.ts";
import { valueText } from "@/library/value-text.ts";
import { pinLabel } from "./pin-label.ts";

/** A part of a node's sentence: words, or a value in its chip, with its type for its dot. */
export type SentencePart = { text: string } | { chip: string; valueType: string };

/** A node of a graph, as the IPC gives it (`ipc::graph`). */
export type GraphNode = NodeView_Serialize;

/**
 * A node's sentence (`node.kinds.<kind>.sentence`), its holes filled with what feeds each: what a wire brings, said by
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
  // The sentence as written, its holes kept: the player's language, else English.
  const key = `node.kinds.${node.kind}.sentence`;
  const raw = i18next.getResource(language, "translation", key) ?? i18next.getResource("en", "translation", key);
  if (node.family === null || typeof raw !== "string") {
    return [{ text: i18next.t("blueprint.unknownKind", { kind: node.kind }) }];
  }
  const sentence = raw;
  const parts: SentencePart[] = [];
  for (const [index, piece] of sentence.split(/\{\{(\w+)\}\}/).entries()) {
    if (index % 2 === 0) {
      if (piece !== "") {
        parts.push({ text: piece });
      }
      continue;
    }
    const pin = node.pins.find((one) => one.id === piece && one.side === "in");
    const valueType = pin?.carries ?? "text";
    const typed = node.inputs[piece];
    const chip = wired.get(piece) ?? valueText(valueType, typed ?? pin?.default ?? null, language);
    parts.push({ chip, valueType });
  }
  return parts;
}

/** A sentence read as plain words, its chips' values among them. */
function plain(parts: SentencePart[]): string {
  return parts.map((part) => ("text" in part ? part.text : part.chip)).join("");
}

/**
 * Every node's sentence in `graph`, each wired input saying what it brings: a variable's name, a query's own sentence
 * ("Ressources de Métier"), else its source pin's label ("Récoltes"). A loop of queries, which no graph should hold,
 * says the pin's label.
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
      const label = pinLabel(wire.from[1]);
      if (source?.does === "get") {
        wired.set(wire.to[1], names.get(source.variable) ?? source.variable);
      } else if (source?.family === "query" && !reading.has(source.id)) {
        wired.set(wire.to[1], plain(sentence(source)));
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
