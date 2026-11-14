import i18next from "i18next";

import type { ProblemView } from "@/bindings.ts";

/**
 * A problem said in the player's language (`blueprint.problem.<kind>`), with the id it names and its pin; `names`
 * gives a variable's or a function's name where the problem names one.
 */
export function problemText(problem: ProblemView, names: ReadonlyMap<string, string>): string {
  const subject = problem.subject === null ? "" : (names.get(problem.subject) ?? problem.subject);
  // The problems' kinds are typed, so each has its key; their names are read at run time.
  return String(i18next.t(`blueprint.problem.${problem.kind}` as never, { subject, pin: problem.pin ?? "" }));
}
