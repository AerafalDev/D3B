// The AppImage's own pieces, which no Debian package gives: the type2-runtime appimagetool prepends and the libraries
// it links statically, AppImageKit's AppRun, linuxdeploy's AppRun script and the GTK plugin's hook. What the build
// used is read from it by scripts/appimage-notices.ts; this file knows each piece's licence and where the text of that
// licence is, at the version used (docs/updates.md).

export type Result<T> = { ok: true; value: T } | { ok: false; error: string };

/** One piece of the AppImage that no Debian package gives, with where its licence's text is. */
export type OwnPiece = {
  /** Its name and version, such as `libfuse 3.15.0`. */
  label: string;
  licence: string;
  /** What it is in the AppImage. */
  role: string;
  /** The URLs of its licence's texts, at the version the AppImage holds. */
  texts: string[];
  /** The files of `/usr/share/common-licenses` its licence's texts refer to instead of quoting them. */
  commonLicenses: string[];
  /** Where its exact source is, for the pieces whose licence asks for it. */
  source?: string;
};

/** What type2-runtime's build scripts say of a runtime, at the commit the AppImage names. */
export type RuntimeBuild = {
  commit: string;
  alpine: string;
  /** The libraries its Makefile links, as `-l` names them. */
  libraries: string[];
  libfuse: { version: string; sha256: string; patches: string[] };
  squashfuse: { version: string; sha256: string };
};

const RUNTIME = "AppImage/type2-runtime";

/** The files of type2-runtime the notices read, by what they say. */
export const RUNTIME_FILES = {
  makefile: "src/runtime/Makefile",
  dependencies: "scripts/common/install-dependencies.sh",
  dockerfile: "scripts/docker/Dockerfile",
} as const;

/** A file of a GitHub repository at a tag or a commit, which never changes. */
export function rawUrl(repo: string, ref: string, path: string): string {
  return `https://raw.githubusercontent.com/${repo}/${ref}/${path}`;
}

/** The runtime's commit, from what `<AppImage> --appimage-version` prints (type2-runtime's CI writes it in). */
export function runtimeCommit(versionOutput: string): Result<string> {
  const commit = /github\.com\/AppImage\/type2-runtime\/commit\/([0-9a-f]{7,40})\s*$/m.exec(versionOutput)?.[1];
  return commit === undefined
    ? { ok: false, error: `The AppImage's runtime names no type2-runtime commit: ${versionOutput.trim()}` }
    : { ok: true, value: commit };
}

/** The runtime's libraries and their versions, from its Makefile, its dependency script and its Dockerfile. */
export function runtimeBuild(
  commit: string,
  files: { makefile: string; dependencies: string; dockerfile: string },
): Result<RuntimeBuild> {
  // Every -l of the Makefile, wherever it is, so that a continued line, `LIBS +=` or LDFLAGS cannot hide a library.
  const libs = /^LIBS\s*[:+?]?=/m.test(files.makefile) ? files.makefile : undefined;
  const alpine = /^FROM alpine:(\S+)\s*$/m.exec(files.dockerfile)?.[1];
  const fuse = /libfuse\/releases\/download\/fuse-([\d.]+)\/fuse-\1\.tar\.xz/.exec(files.dependencies)?.[1];
  const squash = /vasi\/squashfuse\/archive\/([\w.]+)\.tar\.gz/.exec(files.dependencies)?.[1];
  const sha256 = (file: string) =>
    [...files.dependencies.matchAll(/([0-9a-f]{64}) +([^\s"']+)/g)].find(([, , name]) => name === file)?.[1];
  const fuseSha = fuse === undefined ? undefined : sha256(`fuse-${fuse}.tar.xz`);
  const squashSha = squash === undefined ? undefined : sha256(`${squash}.tar.gz`);
  if (
    libs === undefined ||
    alpine === undefined ||
    fuse === undefined ||
    squash === undefined ||
    fuseSha === undefined ||
    squashSha === undefined
  ) {
    const missing = [
      libs === undefined && `the LIBS of ${RUNTIME_FILES.makefile}`,
      alpine === undefined && `the Alpine image of ${RUNTIME_FILES.dockerfile}`,
      fuseSha === undefined && `libfuse's tarball and its sha256 in ${RUNTIME_FILES.dependencies}`,
      squashSha === undefined && `squashfuse's tarball and its sha256 in ${RUNTIME_FILES.dependencies}`,
    ].filter((what) => what !== false);
    return { ok: false, error: `type2-runtime ${commit}: no ${missing.join(", no ")}` };
  }
  const libraries = [...new Set([...libs.matchAll(/(?:^|\s)-l([\w+.-]+)/gm)].map(([, name = ""]) => name))];
  const patches = [...new Set([...files.dependencies.matchAll(/patches\/libfuse\/([\w.-]+)/g)].map(([, p = ""]) => p))];
  return {
    ok: true,
    value: {
      commit,
      alpine,
      libraries,
      libfuse: { version: fuse, sha256: fuseSha, patches },
      squashfuse: { version: squash, sha256: squashSha },
    },
  };
}

/**
 * The upstream tag whose licence text stands for each library the runtime takes from Alpine's packages, whose own
 * versions the runtime does not record: those Alpine 3.21 had when type2-runtime fedfad5 was built, 2026-10-19.
 */
const ALPINE_TEXTS = {
  zstd: rawUrl("facebook/zstd", "v1.5.6", "LICENSE"),
  zlib: rawUrl("madler/zlib", "v1.3.1", "LICENSE"),
  mimalloc: rawUrl("microsoft/mimalloc", "v2.1.7", "LICENSE"),
  musl: "https://git.musl-libc.org/cgit/musl/plain/COPYRIGHT?h=v1.2.5",
};

/** The runtime, then each library it links, once; a library this table does not know fails. */
export function runtimePieces(build: RuntimeBuild): Result<OwnPiece[]> {
  const linked = "linked statically into the runtime";
  const fromAlpine = (name: string, licence: string, text: string): OwnPiece => ({
    label: name,
    licence,
    role: `${linked}, from the packages of Alpine Linux ${build.alpine}; its licence text is upstream's at the version Alpine 3.21 packed on 2026-10-19`,
    texts: [text],
    commonLicenses: [],
  });
  const fuse = build.libfuse;
  const squashfuse: OwnPiece = {
    label: `squashfuse ${build.squashfuse.version}`,
    licence: "BSD-2-Clause",
    role: linked,
    texts: [rawUrl("vasi/squashfuse", build.squashfuse.version, "LICENSE")],
    commonLicenses: [],
    source: `https://github.com/vasi/squashfuse/archive/${build.squashfuse.version}.tar.gz (sha256 ${build.squashfuse.sha256})`,
  };
  const table: Record<string, OwnPiece> = {
    fuse3: {
      label: `libfuse ${fuse.version}`,
      licence: "LGPL-2.1, for its library",
      role: `${linked}, with type2-runtime's patches`,
      texts: [rawUrl("libfuse/libfuse", `fuse-${fuse.version}`, "LICENSE")],
      commonLicenses: ["LGPL-2.1"],
      source: [
        `https://github.com/libfuse/libfuse/releases/download/fuse-${fuse.version}/fuse-${fuse.version}.tar.xz (sha256 ${fuse.sha256})`,
        ...fuse.patches.map(
          (patch) => `  patched by https://github.com/${RUNTIME}/blob/${build.commit}/patches/libfuse/${patch}`,
        ),
      ].join("\n"),
    },
    squashfuse,
    squashfuse_ll: squashfuse,
    zstd: fromAlpine("zstd", "BSD-3-Clause, of its choice with GPL-2.0", ALPINE_TEXTS.zstd),
    z: fromAlpine("zlib", "Zlib", ALPINE_TEXTS.zlib),
    mimalloc: fromAlpine("mimalloc", "MIT", ALPINE_TEXTS.mimalloc),
  };
  const unknown = build.libraries.filter((name) => table[name] === undefined);
  if (unknown.length > 0) {
    return {
      ok: false,
      error: `type2-runtime ${build.commit} links ${unknown.map((name) => `-l${name}`).join(", ")}, whose licence the notices do not know: add it to scripts/appimage-runtime-text.ts`,
    };
  }
  const runtime: OwnPiece = {
    label: `type2-runtime ${build.commit}`,
    licence: "MIT",
    role: "the runtime the AppImage starts with, which mounts or extracts it; appimagetool prepends it",
    texts: [rawUrl(RUNTIME, build.commit, "LICENSE")],
    commonLicenses: [],
    source: `https://github.com/${RUNTIME}/tree/${build.commit}, whose scripts build the runtime from the sources below`,
  };
  const libc = fromAlpine("musl", "MIT", ALPINE_TEXTS.musl);
  const libraries = [...new Set(build.libraries.flatMap((name) => table[name] ?? []))];
  return { ok: true, value: [runtime, ...libraries, libc] };
}

/** The sha256 of AppImageKit's AppRun, as tauri-apps' `apprun-old` release mirrors it, by Tauri's architecture. */
const APPIMAGEKIT_APPRUN: Record<string, string> = {
  x86_64: "f30140a43a0a59e46db21bdefdf749b9e9f2c6946e92afabbacf98b8ae73fb4f",
  aarch64: "072f17c0895a85c490282fe5395c5007e5fc75da727e553b3b8fb680feb11578",
};
/** AppImageKit's commit when tauri-apps mirrored its continuous AppRun, 2021-08-10. */
const APPIMAGEKIT_COMMIT = "1681fd84dbe09c7d9b22e13cdb16ea601aa0ec47";
/** linuxdeploy-plugin-gtk's commit for its licence: its LICENSE.txt is the same since 2019. */
const GTK_PLUGIN_COMMIT = "7a3fbc31a9e5075073ff8790f26effbac5f84453";

/** The linuxdeploy Tauri's bundler used, from the file name it caches it under (`linuxdeploy-<commit>-<arch>`). */
export function linuxdeployCommit(cached: string[], arch: string): Result<string> {
  const commits = cached.flatMap((name) => {
    const parts = /^linuxdeploy-([0-9a-f]{7,40})-(.+)\.AppImage$/.exec(name);
    return parts?.[2] === arch && parts[1] !== undefined ? [parts[1]] : [];
  });
  if (commits.length !== 1 || commits[0] === undefined) {
    return {
      ok: false,
      error: `Tauri's tool cache holds ${commits.length} linuxdeploy AppImages for ${arch}, not one: remove the stale ones`,
    };
  }
  return { ok: true, value: commits[0] };
}

/**
 * AppImageKit's AppRun, which Tauri copies in and linuxdeploy renames `AppRun.wrapped`; the `AppRun` script linuxdeploy
 * writes in its place to source the hooks; and the GTK plugin's hook. Any other file there fails.
 */
export function appRunPieces(input: {
  arch: string;
  wrappedSha256: string;
  appRun: string;
  hooks: string[];
  linuxdeploy: string;
  tauriCli: string;
}): Result<OwnPiece[]> {
  if (APPIMAGEKIT_APPRUN[input.arch] !== input.wrappedSha256) {
    return {
      ok: false,
      error: `AppRun.wrapped (sha256 ${input.wrappedSha256}) is not AppImageKit's AppRun-${input.arch} of tauri-apps' apprun-old: Tauri's bundler changed it`,
    };
  }
  if (!input.appRun.startsWith("#! /usr/bin/env bash\n\n# autogenerated by linuxdeploy\n")) {
    return { ok: false, error: "AppRun is not the script linuxdeploy writes" };
  }
  const others = input.hooks.filter((hook) => hook !== "linuxdeploy-plugin-gtk.sh");
  if (others.length > 0 || input.hooks.length === 0) {
    return {
      ok: false,
      error: `apprun-hooks/ holds ${input.hooks.join(", ") || "nothing"}, not the GTK plugin's hook alone`,
    };
  }
  return {
    ok: true,
    value: [
      {
        label: "AppImageKit's AppRun",
        licence: "MIT",
        role: `AppRun.wrapped, which starts the app; Tauri copies in AppRun-${input.arch} of tauri-apps/binary-releases' apprun-old, a mirror of AppImageKit's continuous build of 2021-08-10 (sha256 ${input.wrappedSha256})`,
        texts: [rawUrl("AppImage/AppImageKit", APPIMAGEKIT_COMMIT, "LICENSE")],
        commonLicenses: [],
      },
      {
        label: `linuxdeploy ${input.linuxdeploy}`,
        licence: "MIT",
        role: "AppRun, the script it writes to source the hooks, then start AppRun.wrapped",
        texts: [rawUrl("linuxdeploy/linuxdeploy", input.linuxdeploy, "LICENSE.txt")],
        commonLicenses: [],
      },
      {
        label: "linuxdeploy-plugin-gtk",
        licence: "MIT",
        role: `apprun-hooks/linuxdeploy-plugin-gtk.sh, the hook its script writes, as Tauri CLI ${input.tauriCli} embeds it`,
        texts: [rawUrl("linuxdeploy/linuxdeploy-plugin-gtk", GTK_PLUGIN_COMMIT, "LICENSE.txt")],
        commonLicenses: [],
      },
    ],
  };
}

/** Every licence text the pieces need, once. */
export function textUrls(pieces: OwnPiece[]): string[] {
  return [...new Set(pieces.flatMap((piece) => piece.texts))];
}

/** The section of SYSTEM-LIBRARIES-NOTICES.txt on these pieces, from their licence texts fetched by URL. */
export function ownSection(pieces: OwnPiece[], texts: Map<string, string>): Result<string> {
  const missing = textUrls(pieces).filter((url) => (texts.get(url)?.trim() ?? "") === "");
  if (pieces.length === 0 || missing.length > 0) {
    return { ok: false, error: `The runtime's notices lack ${missing.join(", ") || "every piece"}` };
  }
  const listing = pieces.map((piece) => `${piece.label} (${piece.licence}): ${piece.role}`);
  const owners = (url: string) => pieces.filter((piece) => piece.texts.includes(url)).map((piece) => piece.label);
  const bodies = textUrls(pieces).map((url) => `## ${owners(url).join(", ")}: ${url}\n\n${texts.get(url)?.trim()}`);
  const sources = pieces.flatMap((piece) => (piece.source === undefined ? [] : [`${piece.label}: ${piece.source}`]));
  const common = [...new Set(pieces.flatMap((piece) => piece.commonLicenses))];
  return {
    ok: true,
    value: [
      "# The AppImage's runtime and AppRun",
      "",
      "No package gives these: appimagetool prepends the runtime, which links its libraries statically, and Tauri and",
      "linuxdeploy write AppRun and its hook. Each keeps its own licence, whose text follows as its project has it at the",
      `version used${common.length > 0 ? `; ${common.join(", ")} is among the licences above, by its file name` : ""}.`,
      "",
      listing.join("\n"),
      "",
      bodies.join("\n\n"),
      "",
      "## Their sources",
      "",
      "Together these rebuild the runtime, with a changed libfuse if one wishes, as LGPL-2.1 asks of a static link.",
      "",
      sources.join("\n"),
      "",
    ].join("\n"),
  };
}
