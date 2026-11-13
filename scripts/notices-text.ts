// The text of THIRD-PARTY-NOTICES.txt, from what each build tool says it bundled (scripts/notices.ts reads them).

/** One npm package the front end bundles, with its licence's text. */
export type Package = { name: string; version: string; license: string; text: string };

/** What `cargo about generate --format json` prints, the fields we read. */
export type CargoAbout = {
  licenses: { id: string; text: string; used_by: { crate: { name: string; version: string } }[] }[];
};

/** A package Vite lists under its heading alone: it found no licence file in it. */
export type Bare = { name: string; version: string; license: string };

export type Notices = { ok: true; text: string } | { ok: false; error: string };

const HEADING = /^## (\S+) - (\S+) \((.*)\)$/;

/** The packages `src/index.css` imports, by name: their styles and fonts reach the app outside Vite's JS graph. */
export function importedPackages(css: string): string[] {
  const names = [...css.matchAll(/^@import\s+["']([^"'./][^"']*)["']/gm)].map(([, spec = ""]) => {
    const parts = spec.split("/");
    return spec.startsWith("@") ? parts.slice(0, 2).join("/") : (parts[0] ?? spec);
  });
  return [...new Set(names)];
}

/** Vite's sections, from its "## name - version (licence)" headings, each with the text under it. */
function viteSections(viteLicenses: string): { pkg: Bare; heading: string; body: string }[] {
  return viteLicenses
    .split(/^(?=## )/m)
    .slice(1)
    .flatMap((section) => {
      const [heading = "", ...rest] = section.split("\n");
      const match = HEADING.exec(heading);
      if (match === null) {
        return [];
      }
      const [, name = "", version = "", license = ""] = match;
      return [{ pkg: { name, version, license }, heading, body: rest.join("\n").trim() }];
    });
}

/** The packages Vite lists without a licence text, which the caller must supply. */
export function barePackages(viteLicenses: string): Bare[] {
  return viteSections(viteLicenses)
    .filter((section) => section.body === "")
    .map((section) => section.pkg);
}

/** The MIT licence's text for a package that ships none, naming its author as the copyright holder. */
export function mitText(holder: string): string {
  return `MIT License

Copyright (c) ${holder}

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.`;
}

function packageSection(pkg: Package): string {
  return `## ${pkg.name} - ${pkg.version} (${pkg.license})\n\n${pkg.text.trim()}`;
}

/** One section per licence text, naming every crate under it; MPL-2.0's also says where each crate's source is. */
function crateSection(license: CargoAbout["licenses"][number]): string {
  const crates = license.used_by.map(({ crate }) => `${crate.name} ${crate.version}`).join(", ");
  const sources =
    license.id === "MPL-2.0"
      ? `\n\nThe source of each: ${license.used_by.map(({ crate }) => `https://crates.io/crates/${crate.name}/${crate.version}`).join(", ")}`
      : "";
  return `## ${license.id}, used by: ${crates}${sources}\n\n${license.text.trim()}`;
}

/**
 * The whole file: the interface's code, then its styles and font, then the crates of the binary for `target`.
 * `supplied` holds the texts of the packages Vite lists bare, by name; any one missing fails.
 */
export function noticesText(input: {
  viteLicenses: string;
  supplied: Map<string, string>;
  cssPackages: Package[];
  about: CargoAbout;
  target: string;
}): Notices {
  const sections = viteSections(input.viteLicenses);
  if (sections.length === 0) {
    return { ok: false, error: "Vite's licence file lists no package: is build.license on?" };
  }
  if (input.about.licenses.length === 0) {
    return { ok: false, error: "cargo-about found no crate licence" };
  }
  const code: string[] = [];
  for (const { pkg, heading, body } of sections) {
    const text = body === "" ? input.supplied.get(pkg.name) : body;
    if (text === undefined) {
      return { ok: false, error: `${pkg.name} ships no licence text and none was supplied` };
    }
    code.push(`${heading}\n\n${text.trim()}`);
  }
  const bundled = new Set(sections.map((section) => section.pkg.name));
  const styles = input.cssPackages.filter((pkg) => !bundled.has(pkg.name));
  // The deb and the rpm share this file with the AppImage, the one of the three that packs system libraries.
  const linux = input.target.includes("-linux-")
    ? [
        "The AppImage also packs system libraries (GTK, WebKitGTK and theirs, from Ubuntu's packages): their licences",
        "and where their sources are, are in SYSTEM-LIBRARIES-NOTICES.txt beside this file. The deb and the rpm use",
        "the system's own.",
        "",
      ]
    : [];
  const text = [
    "D3B, third-party notices",
    "",
    "D3B bundles the software below. Each piece keeps its own licence, given in full under its name.",
    "",
    ...linux,
    "# The interface's code (npm packages)",
    "",
    code.join("\n\n"),
    "",
    "# The interface's styles and font (npm packages)",
    "",
    styles.map(packageSection).join("\n\n"),
    "",
    `# The app's binary (Rust crates, ${input.target})`,
    "",
    input.about.licenses.map(crateSection).join("\n\n"),
    "",
  ].join("\n");
  return { ok: true, text };
}
