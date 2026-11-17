import i18next from "i18next";

import type { NodeView_Serialize, ProblemView } from "@/bindings.ts";
import { pinLabel } from "../pin-label.ts";

/**
 * A problem said in the player's language (`blueprint.problem.<kind>`), with the id it names and its pin, labelled as
 * its node draws it ("Alors 2", a case by its value); `names` gives a variable's or a function's name where the problem
 * names one.
 */
export function problemText(
  problem: ProblemView,
  names: ReadonlyMap<string, string>,
  node?: NodeView_Serialize,
): string {
  const subject = problem.subject == null ? "" : (names.get(problem.subject) ?? problem.subject);
  // The problems' kinds are typed, so each has its key; their names are read at run time.
  const pin = problem.pin == null ? "" : pinLabel(problem.pin, node);
  return String(i18next.t(`blueprint.problem.${problem.kind}` as never, { subject, pin }));
}
