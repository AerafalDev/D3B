// Writes src-tauri/gen/SYSTEM-LIBRARIES-NOTICES.txt, the licences of the system libraries the AppImage packs, read
// from a first AppImage built on this machine: the release builds the one it ships right after, with this file in it
// (docs/updates.md). Usage: bun scripts/appimage-notices.ts <target>, on the Debian or Ubuntu machine that builds the
// release, with cargo-about (scripts/notices.ts runs in the build). Exits 1 on failure, 2 if it cannot run.
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readdirSync,
  readFileSync,
  realpathSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import {
  type DebPackage,
  hostCandidates,
  isOurs,
  licenseFiles,
  ownerOf,
  referencedLicenses,
  systemNoticesText,
  usrMergeAliases,
} from "./appimage-notices-text.ts";

const OUT = "src-tauri/gen/SYSTEM-LIBRARIES-NOTICES.txt";

function fail(message: string, code: number): never {
  console.error(message);
  process.exit(code);
}

function run(command: string, args: string[], cwd?: string): { status: number | null; stdout: string } {
  const result = spawnSync(command, args, { encoding: "utf8", cwd, maxBuffer: 64 * 1024 * 1024 });
  return { status: result.error === undefined ? result.status : null, stdout: result.stdout ?? "" };
}

const target = process.argv[2];
if (target === undefined || !target.includes("-linux-")) {
  fail("Usage: bun scripts/appimage-notices.ts <Linux target>, such as x86_64-unknown-linux-gnu", 2);
}
if (process.platform !== "linux" || run("dpkg-query", ["--version"]).status !== 0) {
  fail("The AppImage's notices are read on the Debian or Ubuntu machine that builds it", 2);
}
const multiarch = `${target.split("-")[0]}-linux-gnu`;
// tauri.conf.json is ours; productName is a string there.
const product = (JSON.parse(readFileSync("src-tauri/tauri.conf.json", "utf8")) as { productName: string }).productName;

// The release configuration names this file among the AppImage's: the first build needs it to exist.
mkdirSync(dirname(OUT), { recursive: true });
writeFileSync(OUT, "Written by scripts/appimage-notices.ts from a first build; this one is never shipped.\n");
const build = spawnSync(
  "bun",
  [
    "tauri",
    "build",
    "--target",
    target,
    "--bundles",
    "appimage",
    "--config",
    "src-tauri/tauri.release.conf.json",
    "--config",
    JSON.stringify({ bundle: { createUpdaterArtifacts: false } }),
  ],
  { stdio: "inherit" },
);
if (build.status !== 0) {
  fail("The first AppImage did not build", 1);
}
const bundleDir = `src-tauri/target/${target}/release/bundle/appimage`;
const images = readdirSync(bundleDir).filter((name) => name.endsWith(".AppImage"));
if (images.length !== 1 || images[0] === undefined) {
  fail(`${bundleDir} holds ${images.length} AppImages, not one`, 1);
}
const image = resolve(bundleDir, images[0]);

const scratch = mkdtempSync(join(tmpdir(), "appimage-notices-"));
if (run(image, ["--appimage-extract"], scratch).status !== 0) {
  fail(`${image} did not extract`, 1);
}
const root = join(scratch, "squashfs-root");

function files(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      return files(path);
    }
    // Symbolic links point at packed files, which are listed themselves.
    return entry.isFile() ? [relative(root, path).split("\\").join("/")] : [];
  });
}

function owner(path: string): string | undefined {
  for (const candidate of hostCandidates(path, multiarch).filter((host) => existsSync(host))) {
    for (const host of new Set([...usrMergeAliases(candidate), ...usrMergeAliases(realpathSync(candidate))])) {
      const name = ownerOf(run("dpkg-query", ["-S", host]).stdout);
      if (name !== undefined) {
        return name;
      }
    }
  }
  return undefined;
}

const errors: string[] = [];
const byPackage = new Map<string, string[]>();
for (const path of files(root).filter((file) => !isOurs(file, product))) {
  const name = owner(path);
  if (name === undefined) {
    errors.push(`${path}: no package owns it`);
  } else {
    byPackage.set(name, [...(byPackage.get(name) ?? []), path]);
  }
}

const query = run("dpkg-query", [
  "-W",
  "-f",
  // biome-ignore lint/suspicious/noTemplateCurlyInString: dpkg-query's own format, not a template.
  "${Package}\\t${Version}\\t${source:Package}\\t${source:Version}\\n",
  ...byPackage.keys(),
]);
const meta = new Map(
  query.stdout
    .split("\n")
    .map((line) => line.split("\t"))
    .filter((fields) => fields.length === 4)
    .map(([name = "", version = "", source = "", sourceVersion = ""]) => [name, { version, source, sourceVersion }]),
);

const packages: DebPackage[] = [];
const licenses = new Map<string, string>();
for (const [name, packed] of byPackage) {
  const info = meta.get(name);
  const copyrightPath = `/usr/share/doc/${name}/copyright`;
  if (info === undefined) {
    errors.push(`${name}: dpkg-query gives no version`);
    continue;
  }
  if (!existsSync(copyrightPath)) {
    errors.push(`${name}: ${copyrightPath} is missing`);
    continue;
  }
  const copyright = readFileSync(copyrightPath, "utf8");
  for (const license of referencedLicenses(copyright)) {
    const file = licenseFiles(license).find((candidate) => existsSync(`/usr/share/common-licenses/${candidate}`));
    if (file === undefined) {
      errors.push(`${name}: its copyright cites /usr/share/common-licenses/${license}, which is missing`);
    } else if (!licenses.has(file)) {
      licenses.set(file, readFileSync(`/usr/share/common-licenses/${file}`, "utf8"));
    }
  }
  packages.push({ name, ...info, copyright, files: packed });
}

rmSync(scratch, { recursive: true, force: true });
// The release's own build writes the AppImage it ships in the same place.
rmSync(image);
if (errors.length > 0) {
  fail(errors.join("\n"), 1);
}

const release = readFileSync("/etc/os-release", "utf8");
const distribution = /^PRETTY_NAME="?([^"\n]*)"?$/m.exec(release)?.[1] ?? "the build machine";
writeFileSync(OUT, systemNoticesText({ arch: multiarch, distribution, packages, licenses }));
const count = packages.reduce((sum, pkg) => sum + pkg.files.length, 0);
console.log(`${OUT}: ${count} files from ${packages.length} packages, ${licenses.size} licence texts, for ${target}`);
