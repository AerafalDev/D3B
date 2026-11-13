// The text of SYSTEM-LIBRARIES-NOTICES.txt, the AppImage's own notices, from what a built AppImage packs and the
// Debian packages its files come from (scripts/appimage-notices.ts reads them; docs/updates.md).

/** One Debian package the AppImage takes files from, with its copyright file as the build machine has it. */
export type DebPackage = {
  name: string;
  version: string;
  source: string;
  sourceVersion: string;
  copyright: string;
  /** The files the AppImage packs from it, relative to the AppImage's root. */
  files: string[];
};

/** Files the AppImage's own tools write: no package owns them. */
const GENERATED = new Set([
  "AppRun",
  "AppRun.wrapped",
  "usr/lib/gdk-pixbuf-2.0/2.10.0/loaders.cache",
  "usr/lib/gio/modules/giomodule.cache",
  "usr/lib/gtk-3.0/3.0.0/immodules.cache",
  "usr/share/glib-2.0/schemas/gschemas.compiled",
]);

/**
 * Whether a file of the AppImage is not a system package's: the app's own (its binary, resources, desktop entry and
 * icons), linuxdeploy's AppRun and hooks, the caches the GTK plugin generates, and the copyright files linuxdeploy
 * copies, which the notices carry anyway.
 */
export function isOurs(path: string, product: string): boolean {
  const binary = product.toLowerCase();
  return (
    GENERATED.has(path) ||
    path === `${product}.png` ||
    path.startsWith("apprun-hooks/") ||
    path === `usr/bin/${binary}` ||
    path.startsWith(`usr/lib/${product}/`) ||
    path === `usr/share/applications/${product}.desktop` ||
    new RegExp(`^usr/share/icons/hicolor/[^/]+/apps/${binary}\\.png$`).test(path) ||
    path.startsWith("usr/share/doc/")
  );
}

/**
 * Where a packed file may come from on the build machine: linuxdeploy flattens the libraries it deploys into
 * `usr/lib/`, from the multiarch folder of `/usr/lib` or of `/lib`; anything else keeps its path.
 */
export function hostCandidates(path: string, multiarch: string): string[] {
  if (!path.startsWith("usr/lib/")) {
    return [`/${path}`];
  }
  const rest = path.slice("usr/lib/".length);
  return [`/${path}`, `/usr/lib/${multiarch}/${rest}`, `/lib/${multiarch}/${rest}`];
}

/** Both spellings of a path under a merged `/lib`: dpkg knows each file by the one its package installed. */
export function usrMergeAliases(path: string): string[] {
  if (path.startsWith("/usr/lib/")) {
    return [path, path.slice("/usr".length)];
  }
  if (path.startsWith("/lib/")) {
    return [path, `/usr${path}`];
  }
  return [path];
}

/** The package `dpkg-query -S` says owns a path, without its architecture; the first when several share it. */
export function ownerOf(dpkgOutput: string): string | undefined {
  for (const line of dpkgOutput.split("\n")) {
    const end = line.indexOf(": /");
    if (end <= 0 || line.startsWith("diversion by ")) {
      continue;
    }
    const first = line.slice(0, end).split(", ")[0] ?? "";
    return first.replace(/:.*$/, "");
  }
  return undefined;
}

/** The licences of `/usr/share/common-licenses` a copyright file refers to instead of quoting them, by name. */
export function referencedLicenses(copyright: string): string[] {
  const names = [...copyright.matchAll(/\/usr\/share\/common-licenses\/([A-Za-z][\w+-]*(?:\.\d+)*)/g)].map(
    ([, name = ""]) => name,
  );
  return [...new Set(names)];
}

/**
 * The files of `/usr/share/common-licenses` a cited name may be: as written, then Debian's own name when the citation
 * spells the version SPDX's way (pango's copyright cites `GPL-2.0` and `GPL-3.0`, whose files are `GPL-2` and `GPL-3`).
 */
export function licenseFiles(name: string): string[] {
  return name.endsWith(".0") ? [name, name.slice(0, -".0".length)] : [name];
}

/** The page of a source package's exact version on Launchpad, which keeps every version Ubuntu published. */
export function launchpadUrl(source: string, version: string): string {
  return `https://launchpad.net/ubuntu/+source/${source}/${version}`;
}

/**
 * The whole file: the packages and the files each gives, each copyright file once (packages built from one source
 * often share it), the texts of the common licences they cite, and where each source package is.
 */
export function systemNoticesText(input: {
  arch: string;
  distribution: string;
  packages: DebPackage[];
  licenses: Map<string, string>;
}): string {
  const packages = [...input.packages].sort((a, b) => a.name.localeCompare(b.name));
  const label = (pkg: DebPackage) => `${pkg.name} ${pkg.version}`;
  const listing = packages.map(
    (pkg) =>
      `${label(pkg)} (source ${pkg.source} ${pkg.sourceVersion})\n${pkg.files
        .toSorted()
        .map((file) => `  ${file}`)
        .join("\n")}`,
  );
  const byText = new Map<string, DebPackage[]>();
  for (const pkg of packages) {
    byText.set(pkg.copyright, [...(byText.get(pkg.copyright) ?? []), pkg]);
  }
  const copyrights = [...byText].map(([text, owners]) => `## ${owners.map(label).join(", ")}\n\n${text.trim()}`);
  const licenses = [...input.licenses]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([name, text]) => `## ${name}\n\n${text.trim()}`);
  const sources = new Map(packages.map((pkg) => [`${pkg.source}=${pkg.sourceVersion}`, pkg]));
  const sourceLines = [...sources.values()]
    .sort((a, b) => a.source.localeCompare(b.source))
    .map(
      (pkg) =>
        `${pkg.source} ${pkg.sourceVersion}: ${launchpadUrl(pkg.source, pkg.sourceVersion)}\n  apt-get source ${pkg.source}=${pkg.sourceVersion}`,
    );
  return [
    "D3B, the AppImage's system libraries",
    "",
    `The AppImage for ${input.arch} packs the files below from the packages of ${input.distribution}: GTK, WebKitGTK`,
    "and the libraries, modules and data they need, as linuxdeploy and its GTK plugin chose them. Each package keeps",
    "its own licence: its copyright file follows in full, then the licence texts those files refer to, then where",
    "the source of each package is. The licences of the app itself and of what it bundles are in",
    "THIRD-PARTY-NOTICES.txt, beside this file.",
    "",
    "# The packages and their files",
    "",
    listing.join("\n\n"),
    "",
    "# Their copyright files",
    "",
    copyrights.join("\n\n"),
    "",
    "# The licences they refer to, by their file name in /usr/share/common-licenses",
    "",
    licenses.join("\n\n"),
    "",
    "# Their sources",
    "",
    "Ubuntu's archive keeps the source of every version it published. Each source package below is the exact version",
    "the AppImage was built from: its page on Launchpad holds the files, and `apt-get source` fetches them on an",
    "Ubuntu system with the archive's deb-src lines.",
    "",
    sourceLines.join("\n"),
    "",
  ].join("\n");
}
