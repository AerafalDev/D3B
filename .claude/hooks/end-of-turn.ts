// Stop: the fast checks of what the turn left changed (Biome on the changed web files, cargo check when Rust
// changed). A failure blocks the stop once, with the errors, so the turn does not end on a broken tree.
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";

const root = process.env["CLAUDE_PROJECT_DIR"] ?? process.cwd();

let input: { stop_hook_active?: boolean } = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  // No input: check anyway.
}
if (input.stop_hook_active) {
  process.exit(0);
}

function run(command: string, args: string[]): { ok: boolean; output: string } {
  const result = spawnSync(command, args, { cwd: root, encoding: "utf8", shell: false });
  return { ok: result.status === 0, output: `${result.stdout ?? ""}${result.stderr ?? ""}` };
}

const changed = run("git", ["status", "--porcelain", "--untracked-files=all"])
  .output.split("\n")
  .filter((line) => line.length > 3 && !line.startsWith("D "))
  .map((line) => line.slice(3).trim().replace(/^"|"$/g, ""));

const failures: string[] = [];

const web = changed.filter((path) => /\.(tsx?|json|css)$/.test(path) && !path.startsWith("data/"));
if (web.length > 0) {
  const biome = run("bunx", ["biome", "check", "--no-errors-on-unmatched", ...web]);
  if (!biome.ok) {
    failures.push(`Biome:\n${biome.output.slice(-3000)}`);
  }
}

if (changed.some((path) => path.startsWith("src-tauri/") && /\.(rs|toml)$/.test(path))) {
  const cargo = run("cargo", ["check", "--manifest-path", "src-tauri/Cargo.toml", "--all-targets", "--quiet"]);
  if (!cargo.ok) {
    failures.push(`cargo check:\n${cargo.output.slice(-3000)}`);
  }
}

if (failures.length > 0) {
  console.log(JSON.stringify({ decision: "block", reason: `Fix before ending the turn.\n\n${failures.join("\n\n")}` }));
}
