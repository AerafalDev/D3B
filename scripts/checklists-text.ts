// The cap on open checklists and the size of docs/now.md (CLAUDE.md, Scope; the owner, 2026-11-23): read from the
// texts alone, so that scripts/check-checklists.ts only reads the files and the environment.

/** At most this many checklists open before a new feature PR. */
export const CAP = 5;
/** docs/now.md stays under a page (docs/lessons.md). */
export const NOW_LIMIT = 150;
/** The label of a feature PR that finishes the feature already started, which the cap lets through. */
export const FINISHING = "step-finish";

/** Where a checklist stands (docs/checklists/README.md); only `open` counts toward the cap. */
export type Status = "open" | "waiting" | "measured";

/** A file of `docs/checklists/`, by its file name. */
export type Checklist = { name: string; text: string };

/** The pull request being checked, when CI names it. */
export type PullRequest = { title: string; labels: string[] };

/** A checklist's status from its `Status: …` line, or `undefined` when it has none. */
export function statusOf(text: string): Status | undefined {
  const match = /^Status: (open|waiting|measured)\r?$/m.exec(text);
  // The pattern's group takes one of the three words alone.
  return match?.[1] as Status | undefined;
}

/** Whether a Conventional Commits title is a feature's. */
function isFeature(title: string): boolean {
  return /^feat(\([^)]*\))?!?:/.test(title);
}

/** What breaks the cap or the index, with the number of checklists open and of now.md's lines. */
export function checklistProblems(input: { now: string; checklists: Checklist[]; pr?: PullRequest }): {
  open: number;
  nowLines: number;
  problems: string[];
} {
  const problems: string[] = [];
  const nowLines = input.now.replace(/\r?\n$/, "").split(/\r?\n/).length;
  if (nowLines > NOW_LIMIT) {
    problems.push(
      `docs/now.md: ${nowLines} lines (limit ${NOW_LIMIT}): move what is not the step, the next items or the index out`,
    );
  }
  let open = 0;
  for (const checklist of input.checklists) {
    const status = statusOf(checklist.text);
    if (status === undefined) {
      problems.push(
        `docs/checklists/${checklist.name}: no \`Status: open\`, \`waiting\` or \`measured\` line under its title`,
      );
      continue;
    }
    if (status === "open") {
      open += 1;
    }
    if (status !== "measured" && !input.now.includes(`checklists/${checklist.name}`)) {
      problems.push(`docs/now.md: no line for checklists/${checklist.name} (${status})`);
    }
  }
  const pr = input.pr;
  if (pr !== undefined && open > CAP && isFeature(pr.title) && !pr.labels.includes(FINISHING)) {
    problems.push(
      `${open} checklists open (cap ${CAP}): a new feature waits for the owner's measures; the remaining PRs of the feature already started carry the \`${FINISHING}\` label`,
    );
  }
  return { open, nowLines, problems };
}
