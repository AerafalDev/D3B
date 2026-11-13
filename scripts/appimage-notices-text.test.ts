import { describe, expect, it } from "vitest";
import {
  type DebPackage,
  hostCandidates,
  isOurs,
  launchpadUrl,
  licenseFiles,
  ownerOf,
  referencedLicenses,
  systemNoticesText,
  usrMergeAliases,
  versionsOf,
} from "./appimage-notices-text.ts";

describe("isOurs", () => {
  it("knows the app's files, the AppImage's own and the caches the GTK plugin generates", () => {
    for (const path of [
      "AppRun",
      "AppRun.wrapped",
      "D3B.png",
      "apprun-hooks/linuxdeploy-plugin-gtk.sh",
      "usr/bin/d3b",
      "usr/lib/D3B/THIRD-PARTY-NOTICES.txt",
      "usr/lib/D3B/data/README.md",
      "usr/share/applications/D3B.desktop",
      "usr/share/icons/hicolor/256x256@2/apps/d3b.png",
      "usr/lib/gdk-pixbuf-2.0/2.10.0/loaders.cache",
      "usr/lib/gio/modules/giomodule.cache",
      "usr/lib/gtk-3.0/3.0.0/immodules.cache",
      "usr/share/glib-2.0/schemas/gschemas.compiled",
      "usr/share/doc/libgtk-3-0/copyright",
    ]) {
      expect(isOurs(path, "D3B"), path).toBe(true);
    }
  });

  it("leaves every packed library and data file to its package", () => {
    for (const path of [
      "usr/lib/libgtk-3.so.0",
      "usr/lib/gio/modules/libdconfsettings.so",
      "usr/lib/x86_64-linux-gnu/webkit2gtk-4.1/WebKitWebProcess",
      "usr/share/glib-2.0/schemas/org.gtk.Settings.FileChooser.gschema.xml",
      "usr/share/icons/hicolor/32x32/apps/other.png",
      "usr/bin/d3b-helper",
    ]) {
      expect(isOurs(path, "D3B"), path).toBe(false);
    }
  });
});

describe("hostCandidates", () => {
  it("looks for a flattened library in the multiarch folders, /usr's and /lib's", () => {
    expect(hostCandidates("usr/lib/libbz2.so.1.0", "x86_64-linux-gnu")).toEqual([
      "/usr/lib/libbz2.so.1.0",
      "/usr/lib/x86_64-linux-gnu/libbz2.so.1.0",
      "/lib/x86_64-linux-gnu/libbz2.so.1.0",
    ]);
  });

  it("takes any other file at its own path", () => {
    expect(hostCandidates("usr/share/glib-2.0/schemas/a.xml", "aarch64-linux-gnu")).toEqual([
      "/usr/share/glib-2.0/schemas/a.xml",
    ]);
  });
});

describe("usrMergeAliases", () => {
  it("gives both spellings of a merged /lib", () => {
    expect(usrMergeAliases("/usr/lib/x/libz.so.1")).toEqual(["/usr/lib/x/libz.so.1", "/lib/x/libz.so.1"]);
    expect(usrMergeAliases("/lib/x/libz.so.1")).toEqual(["/lib/x/libz.so.1", "/usr/lib/x/libz.so.1"]);
    expect(usrMergeAliases("/usr/share/a")).toEqual(["/usr/share/a"]);
  });
});

describe("ownerOf", () => {
  it("reads the package dpkg-query -S names, without its architecture", () => {
    expect(ownerOf("libbz2-1.0:amd64: /lib/x86_64-linux-gnu/libbz2.so.1.0\n")).toBe("libbz2-1.0");
    expect(ownerOf("libgtk-3-common: /usr/share/glib-2.0/schemas/org.gtk.gschema.xml\n")).toBe("libgtk-3-common");
  });

  it("takes the first of several packages and skips diversions", () => {
    expect(ownerOf("diversion by dash from: /bin/sh\nlibglib2.0-0:amd64, libglib2.0-0:i386: /usr/lib/a\n")).toBe(
      "libglib2.0-0",
    );
    expect(ownerOf("local diversion from: /usr/lib/a\nlibz1:amd64: /usr/lib/a\n")).toBe("libz1");
  });

  it("finds none in an empty or unrelated answer", () => {
    expect(ownerOf("")).toBeUndefined();
    expect(ownerOf("dpkg-query: no path found matching pattern /x\n")).toBeUndefined();
  });
});

describe("referencedLicenses", () => {
  it("names each licence of /usr/share/common-licenses once, without a sentence's full stop", () => {
    const copyright = `On Debian systems, see /usr/share/common-licenses/LGPL-2.1.
 and /usr/share/common-licenses/GPL-2, or "/usr/share/common-licenses/LGPL-2.1"; also
 /usr/share/common-licenses/Apache-2.0 and /usr/share/common-licenses/GPL.`;
    expect(referencedLicenses(copyright)).toEqual(["LGPL-2.1", "GPL-2", "Apache-2.0", "GPL"]);
  });

  it("finds none in a copyright file that carries its own text", () => {
    expect(referencedLicenses("Permission is hereby granted, free of charge")).toEqual([]);
  });
});

describe("licenseFiles", () => {
  it("tries a cited name as written, then Debian's without the version's .0, as pango cites GPL-2.0", () => {
    expect(licenseFiles("GPL-2.0")).toEqual(["GPL-2.0", "GPL-2"]);
    expect(licenseFiles("Apache-2.0")).toEqual(["Apache-2.0", "Apache-2"]);
  });

  it("tries any other name as written only", () => {
    expect(licenseFiles("LGPL-2.1")).toEqual(["LGPL-2.1"]);
    expect(licenseFiles("GPL")).toEqual(["GPL"]);
  });
});

describe("versionsOf", () => {
  it("reads each package's version and source from dpkg-query -W, once per name", () => {
    const output =
      "libglib2.0-0\t2.72.4\tglib2.0\t2.72.4\nlibglib2.0-0\t2.72.4\tglib2.0\t2.72.4\nlibz1\t1:1.2\tzlib\t1:1.2\n";
    expect([...versionsOf(output)]).toEqual([
      ["libglib2.0-0", { version: "2.72.4", source: "glib2.0", sourceVersion: "2.72.4" }],
      ["libz1", { version: "1:1.2", source: "zlib", sourceVersion: "1:1.2" }],
    ]);
  });

  it("skips a line without its four fields", () => {
    expect(versionsOf("dpkg-query: no packages found matching x\nlibz1\t1.2\n").size).toBe(0);
  });
});

describe("launchpadUrl", () => {
  it("keeps the epoch, as Launchpad does", () => {
    expect(launchpadUrl("libcap2", "1:2.44-1ubuntu0.22.04.3")).toBe(
      "https://launchpad.net/ubuntu/+source/libcap2/1:2.44-1ubuntu0.22.04.3",
    );
  });
});

const KRB5 = { source: "krb5", sourceVersion: "1.19.2-2ubuntu0.10", copyright: "krb5's copyright\n" };
const PACKAGES: DebPackage[] = [
  { name: "libkrb5-3", version: "1.19.2-2ubuntu0.10", ...KRB5, files: ["usr/lib/libkrb5.so.3"] },
  { name: "libk5crypto3", version: "1.19.2-2ubuntu0.10", ...KRB5, files: ["usr/lib/libk5crypto.so.3"] },
  {
    name: "aaa-tools",
    version: "1.0-1",
    source: "zzz",
    sourceVersion: "1.0-1",
    copyright: "zzz's copyright\n",
    files: ["usr/lib/libzzz.so.1"],
  },
  {
    name: "libgtk-3-0",
    version: "3.24.33-1ubuntu2.2",
    source: "gtk+3.0",
    sourceVersion: "3.24.33-1ubuntu2.2",
    copyright: "See /usr/share/common-licenses/LGPL-2.\n",
    files: ["usr/lib/libgtk-3.so.0", "usr/lib/libgdk-3.so.0"],
  },
];
const INPUT = {
  arch: "x86_64-linux-gnu",
  distribution: "Ubuntu 22.04.5 LTS",
  packages: PACKAGES,
  licenses: new Map([["LGPL-2", "GNU LIBRARY GENERAL PUBLIC LICENSE\n"]]),
};

function notices(input: Parameters<typeof systemNoticesText>[0]): string {
  const result = systemNoticesText(input);
  if (!result.ok) {
    throw new Error(result.error);
  }
  return result.text;
}

describe("systemNoticesText", () => {
  it("lists each package with its files, its copyright file, the licences cited and each source", () => {
    const text = notices(INPUT);
    expect(text).toContain("Ubuntu 22.04.5 LTS");
    expect(text).toContain("x86_64-linux-gnu");
    expect(text).toContain("libgtk-3-0 3.24.33-1ubuntu2.2 (source gtk+3.0 3.24.33-1ubuntu2.2)");
    expect(text).toContain("  usr/lib/libgdk-3.so.0");
    expect(text).toContain("See /usr/share/common-licenses/LGPL-2.");
    expect(text).toContain("## LGPL-2\n\nGNU LIBRARY GENERAL PUBLIC LICENSE");
    expect(text).toContain("apt-get source gtk+3.0=3.24.33-1ubuntu2.2");
    expect(text).toContain("https://launchpad.net/ubuntu/+source/gtk+3.0/3.24.33-1ubuntu2.2");
  });

  it("gives a copyright file shared by several packages once, under all their names", () => {
    const text = notices(INPUT);
    expect(text.split("krb5's copyright").length - 1).toBe(1);
    expect(text).toContain("## libk5crypto3 1.19.2-2ubuntu0.10, libkrb5-3 1.19.2-2ubuntu0.10");
  });

  it("names each source package once, in order", () => {
    const text = notices(INPUT);
    expect(text.split("apt-get source krb5=").length - 1).toBe(1);
    expect(text.indexOf("apt-get source gtk+3.0=")).toBeLessThan(text.indexOf("apt-get source krb5="));
    expect(text.indexOf("apt-get source krb5=")).toBeLessThan(text.indexOf("apt-get source zzz="));
  });

  it("fails when no package gives a file, as when every file is taken for the app's own", () => {
    expect(systemNoticesText({ ...INPUT, packages: [] })).toEqual({
      ok: false,
      error: "No system package gives the AppImage a file: the notices would list none",
    });
  });
});
