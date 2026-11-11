// SessionStart: gives the session where the project stands (docs/now.md) and the state of the checkout.
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const root = process.env["CLAUDE_PROJECT_DIR"] ?? process.cwd();

function git(...args: string[]): string {
  const result = spawnSync("git", args, { cwd: root, encoding: "utf8" });
  return result.status === 0 ? result.stdout.trim() : "";
}

let now = "docs/now.md is missing.";
try {
  now = readFileSync(join(root, "docs", "now.md"), "utf8").trim();
} catch {
  // The file is created by the first PR; a checkout without it still starts.
}

const branch = git("branch", "--show-current") || "(detached)";
const changed = git("status", "--porcelain").split("\n").filter(Boolean).length;

console.log(`${now}\n\n---\nBranch: ${branch}; ${changed} changed file(s). Work on a branch from origin/next.`);
