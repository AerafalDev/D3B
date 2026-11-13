// Writes src-tauri/gen/THIRD-PARTY-NOTICES.txt, which the release builds ship beside the app (docs/updates.md).
// Usage: bun scripts/notices.ts [target], after `bun run build`; the target is the Rust triple, Tauri's
// TAURI_ENV_TARGET_TRIPLE or else the host's. Needs cargo-about (src-tauri/about.toml). Exits 1 on failure, 2 if it
// cannot run.
import { spawnSync } from "node:child_process";
import { mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { barePackages, type CargoAbout, importedPackages, mitText, noticesText, type Package } from "./notices-text.ts";

const VITE_LICENSES = "dist/.vite/license.md";
const OUT = "src-tauri/gen/THIRD-PARTY-NOTICES.txt";
const CARGO_ABOUT = "cargo install cargo-about@0.9.3 --locked --features cli";

function fail(message: string, code: number): never {
  console.error(message);
  process.exit(code);
}

type Manifest = { version?: unknown; license?: unknown; author?: unknown };

function manifest(name: string): { version: string; license: string; author: string } {
  // A package.json is an object; each field is checked below rather than trusted.
  const fields = JSON.parse(readFileSync(join("node_modules", name, "package.json"), "utf8")) as Manifest;
  const author =
    typeof fields.author === "object" && fields.author !== null
      ? (fields.author as { name?: unknown }).name
      : fields.author;
  if (typeof fields.version !== "string" || typeof fields.license !== "string") {
    fail(`${name}'s package.json names no version or licence`, 1);
  }
  // The author's name alone: no address in the notices.
  const holder = typeof author === "string" ? author.replace(/\s*[<(].*$/, "") : "";
  return { version: fields.version, license: fields.license, author: holder };
}

function cssPackage(name: string): Package {
  const file = readdirSync(join("node_modules", name)).find((entry) => /^licen[cs]e/i.test(entry));
  if (file === undefined) {
    fail(`${name} has no licence file to carry`, 1);
  }
  const { version, license } = manifest(name);
  return { name, version, license, text: readFileSync(join("node_modules", name, file), "utf8") };
}

/** The text of a package Vite found no licence file in: MIT's with its author, the one licence we can write. */
function suppliedText(name: string): string {
  const { license, author } = manifest(name);
  if (license !== "MIT" || author === "") {
    fail(`${name} ships no licence file, and its licence (${license}) or author is not one we can write`, 1);
  }
  return mitText(author);
}

function hostTarget(): string {
  const rustc = spawnSync("rustc", ["-vV"], { encoding: "utf8" });
  const host = /^host: (\S+)$/m.exec(rustc.stdout ?? "")?.[1];
  if (host === undefined) {
    fail("No target: pass the Rust triple, or install Rust", 2);
  }
  return host;
}

const target = process.argv[2] ?? process.env["TAURI_ENV_TARGET_TRIPLE"] ?? hostTarget();

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
if (about.error !== undefined || about.status === null || /no such command/.test(about.stderr)) {
  fail(`cargo-about cannot run: ${CARGO_ABOUT}`, 2);
}
if (about.status !== 0) {
  fail(about.stderr.trim(), 1);
}

const supplied = new Map(barePackages(viteLicenses).map(({ name }) => [name, suppliedText(name)]));
const cssPackages = importedPackages(readFileSync("src/index.css", "utf8")).map(cssPackage);
// The JSON is cargo-about's own output, whose fields CargoAbout names.
const crates = JSON.parse(about.stdout) as CargoAbout;
const notices = noticesText({ viteLicenses, supplied, cssPackages, about: crates, target });
if (!notices.ok) {
  fail(notices.error, 1);
}
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, notices.text);
const count = crates.licenses.reduce((sum, license) => sum + license.used_by.length, 0);
console.log(`${OUT}: ${count} crates and the interface's packages, for ${target}`);
