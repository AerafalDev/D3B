// Writes src-tauri/gen/SYSTEM-LIBRARIES-NOTICES.txt, the licences of the system libraries the AppImage packs, then
// of its runtime and AppRun, read from a first AppImage built on this machine: the release builds the one it ships
// right after, with this file in it (docs/updates.md). The runtime's build scripts and the licence texts of these
// pieces are fetched from their projects at the versions the AppImage holds. Usage: bun scripts/appimage-notices.ts
// <target>, on the Debian or Ubuntu machine that builds the release, with cargo-about (scripts/notices.ts runs in the
// build). Exits 1 on failure, 2 if it cannot run.
import { spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
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
import { homedir, tmpdir } from "node:os";
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
  versionsOf,
} from "./appimage-notices-text.ts";
import {
  appRunPieces,
  linuxdeployCommit,
  type OwnPiece,
  ownSection,
  type Result,
  RUNTIME_FILES,
  rawUrl,
  runtimeBuild,
  runtimeCommit,
  runtimePieces,
  textUrls,
} from "./appimage-runtime-text.ts";

const OUT = "src-tauri/gen/SYSTEM-LIBRARIES-NOTICES.txt";

function fail(message: string, code: number): never {
  console.error(message);
  process.exit(code);
}

function run(command: string, args: string[], cwd?: string): { status: number | null; stdout: string; stderr: string } {
  const result = spawnSync(command, args, { encoding: "utf8", cwd, maxBuffer: 64 * 1024 * 1024 });
  const status = result.error === undefined ? result.status : null;
  return { status, stdout: result.stdout ?? "", stderr: result.stderr ?? "" };
}

async function fetchText(url: string): Promise<Result<string>> {
  try {
    const response = await fetch(url);
    return response.ok
      ? { ok: true, value: await response.text() }
      : { ok: false, error: `${url}: HTTP ${response.status}` };
  } catch (error) {
    return { ok: false, error: `${url}: ${String(error)}` };
  }
}

const target = process.argv[2];
if (target === undefined || !target.includes("-linux-")) {
  fail("Usage: bun scripts/appimage-notices.ts <Linux target>, such as x86_64-unknown-linux-gnu", 2);
}
if (process.platform !== "linux" || run("dpkg-query", ["--version"]).status !== 0) {
  fail("The AppImage's notices are read on the Debian or Ubuntu machine that builds it", 2);
}
const arch = target.split("-")[0] ?? "";
const multiarch = `${arch}-linux-gnu`;
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
// The runtime answers this itself, before it mounts anything.
const version = run(image, ["--appimage-version"]);

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
const meta = versionsOf(query.stdout);

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

/** The AppImage's runtime and AppRun, read from the build, with their licence texts fetched at the versions used. */
async function ownPieces(): Promise<Result<{ pieces: OwnPiece[]; texts: Map<string, string> }>> {
  const commit = runtimeCommit(`${version.stdout}${version.stderr}`);
  if (!commit.ok) {
    return commit;
  }
  const [makefile, dependencies, dockerfile] = await Promise.all(
    [RUNTIME_FILES.makefile, RUNTIME_FILES.dependencies, RUNTIME_FILES.dockerfile].map((path) =>
      fetchText(rawUrl("AppImage/type2-runtime", commit.value, path)),
    ),
  );
  if (!makefile?.ok || !dependencies?.ok || !dockerfile?.ok) {
    const failed = [makefile, dependencies, dockerfile].flatMap((file) => (file?.ok === false ? [file.error] : []));
    return { ok: false, error: failed.join("\n") };
  }
  const build = runtimeBuild(commit.value, {
    makefile: makefile.value,
    dependencies: dependencies.value,
    dockerfile: dockerfile.value,
  });
  const runtime = build.ok ? runtimePieces(build.value) : build;
  // Tauri's bundler caches its tools there, under names that carry their versions.
  const cache = join(process.env["XDG_CACHE_HOME"] || join(homedir(), ".cache"), "tauri");
  const linuxdeploy = linuxdeployCommit(existsSync(cache) ? readdirSync(cache) : [], arch);
  const hooks = join(root, "apprun-hooks");
  // The CLI's package.json is npm's; its version is a string there.
  const cli = JSON.parse(readFileSync("node_modules/@tauri-apps/cli/package.json", "utf8")) as { version: string };
  const lost = ["AppRun", "AppRun.wrapped"].filter((name) => !existsSync(join(root, name)));
  const appRun: Result<OwnPiece[]> = !linuxdeploy.ok
    ? linuxdeploy
    : lost.length > 0
      ? { ok: false, error: `The AppImage has no ${lost.join(", ")}: Tauri or linuxdeploy changed how they write it` }
      : appRunPieces({
          arch,
          wrappedSha256: createHash("sha256")
            .update(readFileSync(join(root, "AppRun.wrapped")))
            .digest("hex"),
          appRun: readFileSync(join(root, "AppRun"), "utf8"),
          hooks: existsSync(hooks) ? readdirSync(hooks) : [],
          linuxdeploy: linuxdeploy.value,
          tauriCli: cli.version,
        });
  if (!runtime.ok || !appRun.ok) {
    return { ok: false, error: [runtime, appRun].flatMap((r) => (r.ok ? [] : [r.error])).join("\n") };
  }
  const pieces = [...runtime.value, ...appRun.value];
  const fetched = await Promise.all(textUrls(pieces).map(async (url) => [url, await fetchText(url)] as const));
  const failed = fetched.flatMap(([, text]) => (text.ok ? [] : [text.error]));
  if (failed.length > 0) {
    return { ok: false, error: failed.join("\n") };
  }
  return {
    ok: true,
    value: { pieces, texts: new Map(fetched.flatMap(([url, text]) => (text.ok ? [[url, text.value]] : []))) },
  };
}

const own = await ownPieces();
let runtime = "";
if (!own.ok) {
  errors.push(own.error);
} else {
  for (const license of own.value.pieces.flatMap((piece) => piece.commonLicenses)) {
    const path = `/usr/share/common-licenses/${license}`;
    if (!existsSync(path)) {
      errors.push(`The runtime's notices cite ${path}, which is missing`);
    } else if (!licenses.has(license)) {
      licenses.set(license, readFileSync(path, "utf8"));
    }
  }
  const section = ownSection(own.value.pieces, own.value.texts);
  if (section.ok) {
    runtime = section.value;
  } else {
    errors.push(section.error);
  }
}

rmSync(scratch, { recursive: true, force: true });
// The release's own build writes the AppImage it ships in the same place.
rmSync(image);
if (errors.length > 0) {
  fail(errors.join("\n"), 1);
}

const release = readFileSync("/etc/os-release", "utf8");
const distribution = /^PRETTY_NAME="?([^"\n]*)"?$/m.exec(release)?.[1] ?? "the build machine";
const notices = systemNoticesText({ arch: multiarch, distribution, packages, licenses, runtime });
if (!notices.ok) {
  fail(notices.error, 1);
}
writeFileSync(OUT, notices.text);
const count = packages.reduce((sum, pkg) => sum + pkg.files.length, 0);
const pieces = own.ok ? own.value.pieces.length : 0;
console.log(
  `${OUT}: ${count} files from ${packages.length} packages, ${licenses.size} licence texts, ${pieces} pieces of the runtime and AppRun, for ${target}`,
);
