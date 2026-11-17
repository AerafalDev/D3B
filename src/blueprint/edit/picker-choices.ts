import type { ChoiceView_Serialize, NodeFamily } from "@/bindings.ts";
import { type LinkEnd, linkRefusal } from "./link-check.ts";

/** What the picker lists: "Often here" first, then every choice by family. */
export interface PickerList {
  often: ChoiceView_Serialize[];
  all: [NodeFamily, ChoiceView_Serialize[]][];
}

// The order of "All", as the catalogue reads: what starts a flow, what the character does, then the rest.
const FAMILIES: NodeFamily[] = ["event", "action", "flow", "query", "variable", "call", "function"];

// What often comes after a flow output, the owner's fixed table of 2026-11-16: after a failure, warn, wait or finish;
// after another flow, choose, warn or wait.
const AFTER_FAILED = ["notify", "wait", "finish"];
const AFTER_FLOW = ["if", "notify", "wait"];
// From a value, the first nodes that take it (or give it, for an input): enough to choose from at a glance.
const OFTEN_VALUES = 3;

/** A text searched as the player types it: in any case, accents dropped. */
function folded(text: string): string {
  return text
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

/**
 * What the picker lists for `choices` (`ipc::graph`'s, the graph's): with a wire `dragged` from a pin, only the
 * choices with a pin it fits, "Often here" from the owner's table (after a Failed, after another flow) or, from a
 * value, the first that take or give it; then all of them by family. A `search` keeps those whose `words` hold it.
 */
export function pickerChoices(
  choices: readonly ChoiceView_Serialize[],
  dragged: LinkEnd | null,
  search: string,
  words: (choice: ChoiceView_Serialize) => string = () => "",
): PickerList {
  const wanted = folded(search.trim());
  const kept = choices.filter(
    (choice) =>
      (dragged === null ||
        choice.pins.some((pin) => linkRefusal(dragged, { node: "", pin, types: choice.types ?? [] }) === null)) &&
      (wanted === "" || folded(words(choice)).includes(wanted)),
  );
  let often: ChoiceView_Serialize[] = [];
  if (dragged !== null && dragged.pin.carries !== "exec") {
    often = kept.slice(0, OFTEN_VALUES);
  } else if (dragged !== null && dragged.pin.side === "out") {
    const table = dragged.pin.id === "failed" ? AFTER_FAILED : AFTER_FLOW;
    often = table.flatMap((id) => kept.filter((choice) => choice.does === "kind" && choice.kind === id));
  }
  const all = FAMILIES.map((family) => [family, kept.filter((choice) => choice.family === family)] as const)
    .filter(([, inFamily]) => inFamily.length > 0)
    .map(([family, inFamily]): [NodeFamily, ChoiceView_Serialize[]] => [family, inFamily]);
  return { often, all };
}
