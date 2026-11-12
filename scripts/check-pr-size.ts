// The size of the branch against its base, in handwritten lines added or removed (CLAUDE.md, Delivery).
// Usage: bun scripts/check-pr-size.ts [base], `origin/next` by default. Exits 1 over the limit.
import { spawnSync } from "node:child_process";

const LIMIT = 1000;
// Generated, vendored or locked files: they do not count.
const IGNORED = [
  /^bun\.lock$/,
  /Cargo\.lock$/,
  /^src\/bindings\.ts$/,
  /^src\/components\/(ui|flow)\//,
  /^src-tauri\/icons\//,
  /^CHANGELOG\.md$/,
  /^\.claude\/skills\/[^/]+\/(rules|references)\//,
];

const base = process.argv[2] ?? "origin/next";
const diff = spawnSync("git", ["diff", "--numstat", `${base}...HEAD`], { encoding: "utf8" });
if (diff.status !== 0) {
  console.error(diff.stderr.trim() || `git diff against ${base} failed`);
  process.exit(2);
}

let total = 0;
for (const line of diff.stdout.split("\n")) {
  const [added, removed, path] = line.split("\t");
  if (path === undefined || added === "-" || IGNORED.some((pattern) => pattern.test(path))) {
    continue;
  }
  total += Number(added) + Number(removed);
}

console.log(`${total} handwritten lines changed against ${base} (limit ${LIMIT})`);
process.exit(total > LIMIT ? 1 : 0);
