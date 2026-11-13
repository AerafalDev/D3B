// The text of THIRD-PARTY-NOTICES.txt, from what each build tool says it bundled (scripts/notices.ts reads them).

/** One npm package the front end bundles, with its licence's text. */
export type Package = { name: string; version: string; license: string; text: string };

/** What `cargo about generate --format json` prints, the fields we read. */
export type CargoAbout = {
  licenses: { id: string; text: string; used_by: { crate: { name: string; version: string } }[] }[];
};

/** The packages `src/index.css` imports, by name: their styles and fonts reach the app outside Vite's JS graph. */
export function importedPackages(css: string): string[] {
  const names = [...css.matchAll(/^@import\s+["']([^"'./][^"']*)["']/gm)].map(([, spec = ""]) => {
    const parts = spec.split("/");
    return spec.startsWith("@") ? parts.slice(0, 2).join("/") : (parts[0] ?? spec);
  });
  return [...new Set(names)];
}

/** The package names Vite's `build.license` file lists, from its "## name - version (licence)" headings. */
export function viteNames(viteLicenses: string): Set<string> {
  return new Set([...viteLicenses.matchAll(/^## (\S+) - /gm)].map(([, name = ""]) => name));
}

/** Vite's file without its own title and preamble, which the notices replace. */
function viteBody(viteLicenses: string): string {
  const first = viteLicenses.indexOf("\n## ");
  if (first < 0) {
    throw new Error("Vite's licence file lists no package: is build.license on?");
  }
  return viteLicenses.slice(first + 1).trim();
}

function packageSection(pkg: Package): string {
  return `## ${pkg.name} - ${pkg.version} (${pkg.license})\n\n${pkg.text.trim()}`;
}

/** One section per licence text, naming every crate under it, as cargo-about groups them. */
function crateSections(about: CargoAbout): string {
  if (about.licenses.length === 0) {
    throw new Error("cargo-about found no crate licence");
  }
  return about.licenses
    .map((license) => {
      const crates = license.used_by.map(({ crate }) => `${crate.name} ${crate.version}`).join(", ");
      return `## ${license.id}, used by: ${crates}\n\n${license.text.trim()}`;
    })
    .join("\n\n");
}

/** The whole file: the interface's code, then its styles and font, then the crates of the binary for `target`. */
export function noticesText(input: {
  viteLicenses: string;
  cssPackages: Package[];
  about: CargoAbout;
  target: string;
}): string {
  const bundled = viteNames(input.viteLicenses);
  const styles = input.cssPackages.filter((pkg) => !bundled.has(pkg.name));
  return [
    "D3B, third-party notices",
    "",
    "D3B bundles the software below. Each piece keeps its own licence, given in full under its name.",
    "",
    "# The interface's code (npm packages)",
    "",
    viteBody(input.viteLicenses),
    "",
    "# The interface's styles and font (npm packages)",
    "",
    styles.map(packageSection).join("\n\n"),
    "",
    `# The app's binary (Rust crates, ${input.target})`,
    "",
    crateSections(input.about),
    "",
  ].join("\n");
}
