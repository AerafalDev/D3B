import { describe, expect, it } from "vitest";
import { type CargoAbout, importedPackages, noticesText, viteNames } from "./notices-text.ts";

const VITE = `# Licenses

The app bundles dependencies which contain the following licenses:

## lucide-react - 1.72.0 (ISC)

ISC License

Copyright (c) 2026 Lucide Icons and Contributors

## dockview-react - 8.9.0 (MIT)

MIT License
`;

const ABOUT: CargoAbout = {
  licenses: [
    {
      id: "MIT",
      text: "MIT License\n\nCopyright (c) serde\n",
      used_by: [{ crate: { name: "serde", version: "1.0.0" } }, { crate: { name: "tauri", version: "2.0.0" } }],
    },
  ],
};

const INTER = { name: "@fontsource-variable/inter", version: "5.3.0", license: "OFL-1.1", text: "SIL OFL 1.1\n" };
const DOCKVIEW = { name: "dockview-react", version: "8.9.0", license: "MIT", text: "MIT License\n" };

describe("importedPackages", () => {
  it("names each package the stylesheet imports, scoped or with a subpath, once", () => {
    const css = [
      '@import "tailwindcss";',
      '@import "@fontsource-variable/inter";',
      '@import "dockview-react/dist/styles/dockview.css";',
      '@import "shadcn/tailwind.css";',
      '@import "tailwindcss";',
    ].join("\n");
    expect(importedPackages(css)).toEqual(["tailwindcss", "@fontsource-variable/inter", "dockview-react", "shadcn"]);
  });

  it("leaves out the stylesheet's own files", () => {
    expect(importedPackages('@import "./theme.css";\n@import "../base.css";')).toEqual([]);
  });
});

describe("viteNames", () => {
  it("reads the packages Vite lists", () => {
    expect([...viteNames(VITE)]).toEqual(["lucide-react", "dockview-react"]);
  });
});

describe("noticesText", () => {
  const text = noticesText({ viteLicenses: VITE, cssPackages: [INTER, DOCKVIEW], about: ABOUT, target: "x" });

  it("carries the font, Lucide's notice and every crate under its licence", () => {
    expect(text).toContain("## @fontsource-variable/inter - 5.3.0 (OFL-1.1)\n\nSIL OFL 1.1");
    expect(text).toContain("Copyright (c) 2026 Lucide Icons and Contributors");
    expect(text).toContain("## MIT, used by: serde 1.0.0, tauri 2.0.0\n\nMIT License\n\nCopyright (c) serde");
    expect(text).toContain("# The app's binary (Rust crates, x)");
  });

  it("drops Vite's own title and lists a package Vite already has once", () => {
    expect(text).not.toContain("# Licenses");
    expect(text.match(/## dockview-react/g)).toHaveLength(1);
  });

  it("fails when Vite listed nothing, as when build.license is off", () => {
    expect(() => noticesText({ viteLicenses: "", cssPackages: [], about: ABOUT, target: "x" })).toThrow(
      /build\.license/,
    );
  });

  it("fails when cargo-about found no crate", () => {
    expect(() => noticesText({ viteLicenses: VITE, cssPackages: [], about: { licenses: [] }, target: "x" })).toThrow(
      /cargo-about/,
    );
  });
});
