// The folder rule: at most 10 code files per folder, tests and generated components apart (CLAUDE.md, Code).
// Prints every folder over the limit and exits 1, or exits 0.
import { readdirSync } from "node:fs";
import { join, relative } from "node:path";

const LIMIT = 10;
const ROOTS = ["src", "src-tauri/src", "scripts", "tools/extractor/src"];
const CODE = /\.(rs|ts|tsx|cs)$/;
const TEST = /(\.test\.tsx?$|^tests?\.rs$)/;
// Copied from the shadcn and React Flow registries, updated by their CLI rather than by hand.
const EXEMPT = new Set(["src/components/ui", "src/components/flow"]);

function over(folder: string): string[] {
  const entries = readdirSync(folder, { withFileTypes: true });
  const here = relative(".", folder).replaceAll("\\", "/");
  const code = entries.filter((entry) => entry.isFile() && CODE.test(entry.name) && !TEST.test(entry.name));
  const found = !EXEMPT.has(here) && code.length > LIMIT ? [`${here}: ${code.length} code files (limit ${LIMIT})`] : [];
  const children = entries.filter((entry) => entry.isDirectory() && entry.name !== "tests" && entry.name !== "target");
  return found.concat(children.flatMap((entry) => over(join(folder, entry.name))));
}

const failures = ROOTS.flatMap(over);
for (const failure of failures) {
  console.error(failure);
}
process.exit(failures.length > 0 ? 1 : 0);
