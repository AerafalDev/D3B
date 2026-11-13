// Writes src-tauri/gen/THIRD-PARTY-NOTICES.txt, which the release builds ship beside the app (docs/updates.md).
// Usage: bun scripts/notices.ts [target], after `bun run build`; the target is the Rust triple, Tauri's
// TAURI_ENV_TARGET_TRIPLE by default. Needs cargo-about (src-tauri/about.toml). Exits 1 on failure, 2 if it cannot run.
import { spawnSync } from "node:child_process";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { type CargoAbout, importedPackages, noticesText, type Package } from "./notices-text.ts";

const VITE_LICENSES = "dist/.vite/license.md";
const OUT = "src-tauri/gen/THIRD-PARTY-NOTICES.txt";

function fail(message: string, code: number): never {
  console.error(message);
  process.exit(code);
}

function cssPackage(name: string): Package {
  const root = join("node_modules", name);
  const manifest = JSON.parse(readFileSync(join(root, "package.json"), "utf8")) as Package;
  const file = readdirSync(root).find((entry) => /^licen[cs]e/i.test(entry));
  if (file === undefined) {
    fail(`${name} has no licence file to carry`, 1);
  }
  return { name, version: manifest.version, license: manifest.license, text: readFileSync(join(root, file), "utf8") };
}

const target = process.argv[2] ?? process.env["TAURI_ENV_TARGET_TRIPLE"];
if (target === undefined) {
  fail("No target: pass the Rust triple, or run as Tauri's beforeBuildCommand", 2);
}

let viteLicenses: string;
try {
  viteLicenses = readFileSync(VITE_LICENSES, "utf8");
} catch {
  fail(`${VITE_LICENSES} is missing: run \`bun run build\` first`, 2);
}

const about = spawnSync(
  "cargo",
  [
    "about",
    "generate",
    "--format",
    "json",
    "--fail",
    "--manifest-path",
    "src-tauri/Cargo.toml",
    "--config",
    "src-tauri/about.toml",
    "--target",
    target,
  ],
  { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 },
);
if (about.error !== undefined || about.status === null) {
  fail("cargo-about cannot run: cargo install cargo-about@0.9.3 --locked --features cli", 2);
}
if (about.status !== 0) {
  fail(about.stderr.trim(), 1);
}

const cssPackages = importedPackages(readFileSync("src/index.css", "utf8")).map(cssPackage);
// The JSON is cargo-about's own output, whose fields CargoAbout names.
const crates = JSON.parse(about.stdout) as CargoAbout;
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, noticesText({ viteLicenses, cssPackages, about: crates, target }));
const count = crates.licenses.reduce((sum, license) => sum + license.used_by.length, 0);
console.log(`${OUT}: ${count} crates and the interface's packages, for ${target}`);
