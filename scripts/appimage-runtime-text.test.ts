import { describe, expect, it } from "vitest";
import {
  appRunPieces,
  linuxdeployCommit,
  type OwnPiece,
  ownSection,
  type RuntimeBuild,
  runtimeBuild,
  runtimeCommit,
  runtimePieces,
  textUrls,
} from "./appimage-runtime-text.ts";

const SHA_FUSE = "70589cfd5e1cff7ccd6ac91c86c01be340b227285c5e200baa284e401eea2ca0";
const SHA_SQUASH = "db0238c5981dabbd80ee09ae15387f390091668ca060a7bc38047912491443d3";
const FILES = {
  makefile: "CC = clang\nLIBS          = -lsquashfuse -lsquashfuse_ll -lzstd -lz -lfuse3 -lmimalloc\n",
  dependencies: [
    "wget https://github.com/libfuse/libfuse/releases/download/fuse-3.15.0/fuse-3.15.0.tar.xz",
    `echo "${SHA_FUSE}  fuse-3.15.0.tar.xz" | sha256sum -c -`,
    'patch -p1 < "$this_dir"/../../patches/libfuse/mount.c.diff',
    'wget "https://github.com/vasi/squashfuse/archive/0.5.2.tar.gz"',
    `echo "${SHA_SQUASH}  0.5.2.tar.gz" | sha256sum -c -`,
  ].join("\n"),
  dockerfile: "FROM alpine:3.21\n\nRUN apk add zstd-static\n",
};

function value<T>(result: { ok: true; value: T } | { ok: false; error: string }): T {
  if (!result.ok) {
    throw new Error(result.error);
  }
  return result.value;
}

const BUILD: RuntimeBuild = value(runtimeBuild("fedfad5", FILES));

describe("runtimeCommit", () => {
  it("reads the commit the runtime prints for --appimage-version", () => {
    expect(
      runtimeCommit("AppImage runtime version: https://github.com/AppImage/type2-runtime/commit/fedfad5\n"),
    ).toEqual({ ok: true, value: "fedfad5" });
  });

  it("fails on a runtime built outside type2-runtime's CI", () => {
    expect(runtimeCommit("AppImage runtime version: UNSUPPORTED_LOCAL_DEVELOPER_BUILD\n")).toEqual({
      ok: false,
      error:
        "The AppImage's runtime names no type2-runtime commit: AppImage runtime version: UNSUPPORTED_LOCAL_DEVELOPER_BUILD",
    });
  });
});

describe("runtimeBuild", () => {
  it("reads the libraries, Alpine's version, and libfuse's and squashfuse's tarballs with their patches", () => {
    expect(BUILD).toEqual({
      commit: "fedfad5",
      alpine: "3.21",
      libraries: ["squashfuse", "squashfuse_ll", "zstd", "z", "fuse3", "mimalloc"],
      libfuse: { version: "3.15.0", sha256: SHA_FUSE, patches: ["mount.c.diff"] },
      squashfuse: { version: "0.5.2", sha256: SHA_SQUASH },
    });
  });

  it("fails, naming what it misses, when the scripts change shape", () => {
    const dependencies = FILES.dependencies.replace(`${SHA_FUSE}  fuse-3.15.0.tar.xz`, "");
    expect(runtimeBuild("fedfad5", { ...FILES, dependencies, dockerfile: "FROM debian:12\n" })).toEqual({
      ok: false,
      error:
        "type2-runtime fedfad5: no the Alpine image of scripts/docker/Dockerfile, no libfuse's tarball and its sha256 in scripts/common/install-dependencies.sh",
    });
    const squashless = FILES.dependencies.replace("vasi/squashfuse/archive/0.5.2.tar.gz", "elsewhere");
    expect(runtimeBuild("fedfad5", { ...FILES, makefile: "CC = clang\n", dependencies: squashless })).toEqual({
      ok: false,
      error:
        "type2-runtime fedfad5: no the LIBS of src/runtime/Makefile, no squashfuse's tarball and its sha256 in scripts/common/install-dependencies.sh",
    });
  });

  it("finds a library linked on a continued line, by LIBS += or in LDFLAGS", () => {
    const makefile = "LIBS = -lsquashfuse \\\n  -lzstd\nLIBS += -llzma\nLDFLAGS = -static -llz4\n";
    expect(value(runtimeBuild("fedfad5", { ...FILES, makefile })).libraries).toEqual([
      "squashfuse",
      "zstd",
      "lzma",
      "lz4",
    ]);
  });
});

describe("runtimePieces", () => {
  it("gives the runtime, each library once, and musl, with libfuse's source and patches", () => {
    const pieces = value(runtimePieces(BUILD));
    expect(pieces.map((piece) => piece.label)).toEqual([
      "type2-runtime fedfad5",
      "squashfuse 0.5.2",
      "zstd",
      "zlib",
      "libfuse 3.15.0",
      "mimalloc",
      "musl",
    ]);
    const fuse = pieces.find((piece) => piece.label === "libfuse 3.15.0");
    expect(fuse?.commonLicenses).toEqual(["LGPL-2.1"]);
    expect(fuse?.texts).toEqual(["https://raw.githubusercontent.com/libfuse/libfuse/fuse-3.15.0/LICENSE"]);
    expect(fuse?.source).toContain(`fuse-3.15.0.tar.xz (sha256 ${SHA_FUSE})`);
    expect(fuse?.source).toContain(
      "https://github.com/AppImage/type2-runtime/blob/fedfad5/patches/libfuse/mount.c.diff",
    );
    expect(pieces[0]?.texts).toEqual(["https://raw.githubusercontent.com/AppImage/type2-runtime/fedfad5/LICENSE"]);
    expect(pieces.find((piece) => piece.label === "zstd")?.role).toContain("Alpine Linux 3.21");
  });

  it("fails on a library whose licence it does not know", () => {
    expect(runtimePieces({ ...BUILD, libraries: [...BUILD.libraries, "lzma", "lz4"] })).toEqual({
      ok: false,
      error:
        "type2-runtime fedfad5 links -llzma, -llz4, whose licence the notices do not know: add it to scripts/appimage-runtime-text.ts",
    });
  });
});

describe("linuxdeployCommit", () => {
  it("reads the commit of the one linuxdeploy cached for the architecture", () => {
    const cached = [
      "AppRun-x86_64",
      "linuxdeploy-07333c6-x86_64.AppImage",
      "linuxdeploy-07333c6-aarch64.AppImage",
      "linuxdeploy-plugin-appimage.AppImage",
      "linuxdeploy-plugin-gtk.sh",
    ];
    expect(linuxdeployCommit(cached, "x86_64")).toEqual({ ok: true, value: "07333c6" });
  });

  it("fails when two versions are cached, as after Tauri changed it", () => {
    const cached = ["linuxdeploy-07333c6-x86_64.AppImage", "linuxdeploy-2b73a2b-x86_64.AppImage"];
    expect(linuxdeployCommit(cached, "x86_64")).toEqual({
      ok: false,
      error: "Tauri's tool cache holds 2 linuxdeploy AppImages for x86_64, not one: remove the stale ones",
    });
    expect(linuxdeployCommit([], "aarch64").ok).toBe(false);
  });
});

const SCRIPT = '#! /usr/bin/env bash\n\n# autogenerated by linuxdeploy\n\nexec "$this_dir"/AppRun.wrapped "$@"\n';
const APPRUN = {
  arch: "aarch64",
  wrappedSha256: "072f17c0895a85c490282fe5395c5007e5fc75da727e553b3b8fb680feb11578",
  appRun: SCRIPT,
  hooks: ["linuxdeploy-plugin-gtk.sh"],
  linuxdeploy: "07333c6",
  tauriCli: "2.12.4",
};

describe("appRunPieces", () => {
  it("names AppImageKit's AppRun, linuxdeploy's script and the GTK plugin's hook", () => {
    const pieces = value(appRunPieces(APPRUN));
    expect(pieces.map((piece) => piece.label)).toEqual([
      "AppImageKit's AppRun",
      "linuxdeploy 07333c6",
      "linuxdeploy-plugin-gtk",
    ]);
    expect(pieces[0]?.role).toContain("AppRun-aarch64");
    expect(pieces[1]?.texts).toEqual(["https://raw.githubusercontent.com/linuxdeploy/linuxdeploy/07333c6/LICENSE.txt"]);
    expect(pieces[2]?.role).toContain("Tauri CLI 2.12.4");
  });

  it("fails on another AppRun.wrapped, such as the other architecture's", () => {
    expect(appRunPieces({ ...APPRUN, arch: "x86_64" })).toEqual({
      ok: false,
      error:
        "AppRun.wrapped (sha256 072f17c0895a85c490282fe5395c5007e5fc75da727e553b3b8fb680feb11578) is not AppImageKit's AppRun-x86_64 of tauri-apps' apprun-old: Tauri's bundler changed it",
    });
  });

  it("fails on an AppRun linuxdeploy did not write, or another hook", () => {
    expect(appRunPieces({ ...APPRUN, appRun: "\u007fELF" })).toEqual({
      ok: false,
      error: "AppRun is not the script linuxdeploy writes",
    });
    expect(
      appRunPieces({ ...APPRUN, hooks: ["linuxdeploy-plugin-gtk.sh", "linuxdeploy-plugin-gstreamer.sh"] }),
    ).toEqual({
      ok: false,
      error:
        "apprun-hooks/ holds linuxdeploy-plugin-gtk.sh, linuxdeploy-plugin-gstreamer.sh, not the GTK plugin's hook alone",
    });
    expect(appRunPieces({ ...APPRUN, hooks: [] }).ok).toBe(false);
  });
});

describe("ownSection", () => {
  const pieces: OwnPiece[] = [...value(runtimePieces(BUILD)), ...value(appRunPieces(APPRUN))];
  const texts = new Map(textUrls(pieces).map((url) => [url, `The licence at ${url}\n`]));

  it("lists each piece, quotes each licence text once under its pieces, and gives the sources", () => {
    const text = value(ownSection(pieces, texts));
    expect(text).toContain("# The AppImage's runtime and AppRun");
    expect(text).toContain("libfuse 3.15.0 (LGPL-2.1, for its library): linked statically into the runtime");
    expect(text).toContain("LGPL-2.1 is among the licences above");
    const squash = "https://raw.githubusercontent.com/vasi/squashfuse/0.5.2/LICENSE";
    expect(text).toContain(`## squashfuse 0.5.2: ${squash}\n\nThe licence at ${squash}`);
    expect(text.split(`The licence at ${squash}`).length - 1).toBe(1);
    expect(text).toContain("type2-runtime fedfad5: https://github.com/AppImage/type2-runtime/tree/fedfad5");
    expect(text).toContain("squashfuse 0.5.2: https://github.com/vasi/squashfuse/archive/0.5.2.tar.gz");
  });

  it("fails when a licence text was not fetched or is empty", () => {
    const zlib = "https://raw.githubusercontent.com/madler/zlib/v1.3.1/LICENSE";
    const musl = "https://git.musl-libc.org/cgit/musl/plain/COPYRIGHT?h=v1.2.5";
    const partial = new Map(
      [...texts].filter(([url]) => url !== zlib).map(([url, t]) => [url, url === musl ? " \n" : t]),
    );
    expect(ownSection(pieces, partial)).toEqual({ ok: false, error: `The runtime's notices lack ${zlib}, ${musl}` });
    expect(ownSection([], texts).ok).toBe(false);
  });
});
