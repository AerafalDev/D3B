// The cap on open checklists and the size of docs/now.md (CLAUDE.md, The cap). Usage: bun scripts/check-checklists.ts;
// in CI's pull-request job, PR_TITLE and PR_LABELS (comma-separated) name the PR, and a feature PR over the cap fails.
// Prints the count, every problem, and exits 1 on a problem, 2 when it cannot read the files.
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { CAP, type Checklist, checklistProblems, NOW_LIMIT, type PullRequest } from "./checklists-text.ts";

const FOLDER = "docs/checklists";

let now: string;
let checklists: Checklist[];
try {
  now = readFileSync("docs/now.md", "utf8");
  checklists = readdirSync(FOLDER)
    .filter((name) => name.endsWith(".md") && name !== "README.md")
    .sort()
    .map((name) => ({ name, text: readFileSync(join(FOLDER, name), "utf8") }));
} catch (error) {
  console.error(`cannot read docs/now.md or ${FOLDER}: ${error instanceof Error ? error.message : String(error)}`);
  process.exit(2);
}

const title = process.env["PR_TITLE"];
const pr: PullRequest | undefined =
  title === undefined
    ? undefined
    : { title, labels: (process.env["PR_LABELS"] ?? "").split(",").map((label) => label.trim()) };

const { open, nowLines, problems } = checklistProblems({ now, checklists, ...(pr === undefined ? {} : { pr }) });
console.log(`${open} checklists open (cap ${CAP}); docs/now.md ${nowLines} lines (limit ${NOW_LIMIT})`);
for (const problem of problems) {
  console.error(problem);
}
process.exit(problems.length > 0 ? 1 : 0);
