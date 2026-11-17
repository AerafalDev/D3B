import { cleanup, render, screen, within } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import type { Atlas_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";

// The asset protocol's URL of a file, as Tauri makes it; happy-dom has no Tauri.
vi.mock("@tauri-apps/api/core", () => ({ convertFileSrc: (path: string) => `asset://${path}` }));

const { WorldDetails } = await import("./world-details.tsx");
const { usePlayed } = await import("@/characters/played/played-store.ts");
const { useLibrary } = await import("@/library/library-store.ts");

const names = (fr: string, en = fr) => ({ fr, en });
const ATLAS: Atlas_Serialize = {
  worlds: [],
  jobs: [
    { id: 2, name: names("Bûcheron", "Lumberjack"), icon: "icons/jobs/1.png" },
    { id: 26, name: names("Alchimiste", "Alchemist"), icon: null },
  ],
  resources: [
    { id: 303, name: names("Bois de Frêne", "Ash Wood"), level: 1, job: 2, icon: "icons/items/38017.png" },
    { id: 473, name: names("Bois de Châtaignier"), level: 20, job: 2, icon: "icons/items/38086.png" },
    { id: 421, name: names("Ortie", "Nettle"), level: 1, job: 26, icon: null },
  ],
  areas: [
    {
      id: 0,
      name: names("Astrub"),
      subAreas: [
        {
          id: 97,
          name: names("Forêt d'Astrub", "Astrub Forest"),
          level: 20,
          maps: [
            {
              world: 1,
              at: [
                [0, 0],
                [1, 0],
              ],
              priority: [],
            },
          ],
          resources: [303, 473, 421],
          monsters: 8,
        },
        { id: 4, name: names("Antre d'Astrub"), level: 30, maps: [], resources: [], monsters: 0 },
      ],
    },
  ],
};

describe("the World tab's Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(async () => {
    cleanup();
    await i18next.changeLanguage("fr");
  });

  it("asks for a sub-area while none is chosen", () => {
    render(<WorldDetails atlas={ATLAS} subArea={null} />);
    const details = screen.getByRole("complementary", { name: "Détails" });
    expect(details.textContent).toContain("Choisis une sous-zone dans la vue ou sur la carte.");
  });

  it("says a sub-area's area, level, maps and monsters, and its resources by profession with their icons", () => {
    render(<WorldDetails atlas={ATLAS} subArea={97} />);
    const details = screen.getByRole("complementary", { name: "Détails" });
    expect(details.textContent).toContain("Forêt d'Astrub");
    expect(details.textContent).toContain("Sous-zone du jeu · Astrub");
    const facts = [...details.querySelectorAll("dt")].map(
      (term) => `${term.textContent}: ${term.nextSibling?.textContent}`,
    );
    expect(facts).toEqual(["Niveau: 20", "Cartes: 2", "Monstres: 8 sortes"]);
    const jobs = within(details).getAllByRole("list", { name: /./ });
    expect(jobs.map((job) => job.getAttribute("aria-label"))).toEqual(["Bûcheron", "Alchimiste"]);
    const woods = within(jobs[0] as HTMLElement).getAllByRole("listitem");
    expect(woods.map((wood) => wood.textContent)).toEqual(["Bois de Frêne1", "Bois de Châtaignier20"]);
    expect(woods[0]?.querySelector("img")?.getAttribute("src")).toBe("asset://icons/items/38017.png");
    // The nettle's icon and the alchemist's are missing: neither is drawn.
    expect(jobs[1]?.querySelector("img")).toBeNull();
    expect(details.querySelectorAll("img")).toHaveLength(3);
  });

  it("says whose Zone the sub-area is, and nothing when it is no one's", () => {
    const zone = {
      id: "zone",
      name: "Zone",
      valueType: "zone",
      list: false,
      ranges: false,
      default: null,
      bounds: null,
      memory: false,
      help: "",
    };
    const harvest = { id: "harvest", name: "Harvest", description: "", given: false, version: 1, basedOn: null };
    useLibrary.setState({ blueprints: [{ ...harvest, problems: 0, variables: [{ ...zone, perCharacter: true }] }] });
    const character = { level: 1, breedName: null, head: null, sex: null, lastSeen: 0, place: null };
    const played = (name: string, subArea: number) => ({
      ...character,
      key: name,
      name,
      blueprint: "harvest",
      settings: { zone: JSON.stringify({ sub_area: subArea }) },
      dropped: [],
    });
    usePlayed.setState({ played: [played("Kalyx", 97), played("Orrin", 97), played("Lyra", 4)] });
    render(<WorldDetails atlas={ATLAS} subArea={97} />);
    const details = screen.getByRole("complementary", { name: "Détails" });
    const facts = [...details.querySelectorAll("dt")].map(
      (term) => `${term.textContent}: ${term.nextSibling?.textContent}`,
    );
    expect(facts).toContain("Zone de: Kalyx et Orrin");
    cleanup();
    usePlayed.setState({ played: [played("Lyra", 4)] });
    render(<WorldDetails atlas={ATLAS} subArea={97} />);
    expect(screen.getByRole("complementary", { name: "Détails" }).textContent).not.toContain("Zone de");
    usePlayed.setState({ played: [] });
    useLibrary.setState({ blueprints: [] });
  });

  it("says when a sub-area is on no map, holds no resource and no monster", () => {
    render(<WorldDetails atlas={ATLAS} subArea={4} />);
    const details = screen.getByRole("complementary", { name: "Détails" });
    const facts = [...details.querySelectorAll("dd")].map((value) => value.textContent);
    expect(facts).toEqual(["30", "Pas sur la carte", "Aucun"]);
    expect(details.textContent).toContain("Aucune ressource.");
  });

  it("names everything in the player's language", async () => {
    await i18next.changeLanguage("en");
    render(<WorldDetails atlas={ATLAS} subArea={97} />);
    const details = screen.getByRole("complementary", { name: "Details" });
    expect(details.textContent).toContain("Game sub-area · Astrub");
    expect(details.textContent).toContain("Ash Wood");
    expect(details.textContent).toContain("8 kinds");
  });

  it("leaves out a resource the atlas lacks, and names a profession it lacks by its number", () => {
    const forest = ATLAS.areas[0]?.subAreas[0];
    if (forest === undefined) {
      throw new Error("the fixture's forest");
    }
    const atlas: Atlas_Serialize = {
      ...ATLAS,
      resources: [...ATLAS.resources, { id: 600, name: names("Truite"), level: 1, job: 36, icon: null }],
      areas: [{ id: 0, name: names("Astrub"), subAreas: [{ ...forest, resources: [999, 600] }] }],
    };
    render(<WorldDetails atlas={atlas} subArea={97} />);
    const details = screen.getByRole("complementary", { name: "Détails" });
    const jobs = within(details).getAllByRole("list", { name: /./ });
    expect(jobs.map((job) => [job.getAttribute("aria-label"), job.textContent])).toEqual([["36", "Truite1"]]);
  });
});
