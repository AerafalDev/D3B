// Puts the game data of one client build in data/, from its `client-<build>` release (docs/data.md).
// Usage: bun scripts/fetch-data.ts <build>. Needs the GitHub CLI, signed in or with GH_TOKEN set.
import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, rmSync } from "node:fs";
import { join } from "node:path";

const build = process.argv[2];
if (build === undefined || !/^\d+(\.\d+){1,3}$/.test(build)) {
  console.error("usage: bun scripts/fetch-data.ts <client build, such as 3.7.13.17>");
  process.exit(2);
}

const target = "data";
const archive = join(target, `client-${build}.tar.gz`);
for (const entry of readdirSync(target)) {
  if (entry !== "README.md") {
    rmSync(join(target, entry), { recursive: true, force: true });
  }
}
mkdirSync(target, { recursive: true });

function run(command: string, args: string[]): void {
  const result = spawnSync(command, args, { stdio: "inherit" });
  if (result.status !== 0) {
    console.error(`${command} ${args.join(" ")} failed`);
    process.exit(1);
  }
}

run("gh", ["release", "download", `client-${build}`, "--pattern", `client-${build}.tar.gz`, "--dir", target]);
run("tar", ["-xzf", archive, "-C", target]);
rmSync(archive);

if (!existsSync(join(target, "manifest.json"))) {
  console.error(`client-${build}.tar.gz has no manifest.json at its root`);
  process.exit(1);
}
console.log(`data/ holds the data of client ${build}`);
