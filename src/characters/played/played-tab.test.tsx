import { cleanup, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { type CharacterView, useCharacters } from "../character-store.ts";
import { type PlayedCharacter, usePlayed } from "./played-store.ts";

// The live tab, faked: which connection it shows is all that matters here.
vi.mock("../character-tab.tsx", () => ({
  CharacterTab: ({ params }: { params: { connection: number } }) => <p>live {params.connection}</p>,
}));

const { PlayedTab } = await import("./played-tab.tsx");

const NOW = new Date(2026, 10, 14, 12, 0).getTime();

const kalyx: PlayedCharacter = {
  key: "294-2",
  name: "Kalyx",
  level: 54,
  breedName: { fr: "Iop", en: "Iop" },
  head: null,
  sex: "male",
  lastSeen: new Date(2026, 10, 14, 10, 40).getTime(),
  place: { mapId: 1, at: { x: 5, y: -18 }, subArea: { fr: "Astrub", en: "Astrub" }, area: null },
};

const titles: string[] = [];

function tab() {
  // The tab reads its params and sets its title; the rest of the panel's API plays no part.
  const api = { setTitle: (title: string) => titles.push(title) };
  const props = { api, params: { key: "294-2", icon: "user" } } as unknown as Parameters<typeof PlayedTab>[0];
  return render(<PlayedTab {...props} />);
}

describe("the tab of a character played whose client is closed", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    titles.length = 0;
    useCharacters.setState({ views: {} });
  });
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("says who it is, that it is offline, and when and where it was last seen, without a map or gauges", () => {
    usePlayed.setState({ played: [kalyx] });
    const { container } = tab();
    expect(screen.getByRole("heading").textContent).toBe("Kalyx");
    expect(container.textContent).toContain("Level 54 · Iop · Offline");
    expect(container.textContent?.replace(/[  ]/g, " ")).toContain("Last seen today at 10:40 AM, in Astrub (5, -18).");
    expect(container.querySelector("svg, [role='meter'], [role='progressbar']")).toBeNull();
    expect(titles).toEqual(["Kalyx"]);
  });

  it("says what it knows of where it was last seen", () => {
    usePlayed.setState({ played: [{ ...kalyx, place: { ...kalyx.place, at: null } as PlayedCharacter["place"] }] });
    const first = tab();
    expect(first.container.textContent).toMatch(/Last seen today at .*, in Astrub\.$/);
    first.unmount();
    usePlayed.setState({ played: [{ ...kalyx, place: null }] });
    expect(tab().container.textContent).toMatch(/Last seen today at [^,]*\.$/);
  });

  it("becomes the character's live tab once it plays again", () => {
    usePlayed.setState({ played: [kalyx] });
    useCharacters.setState({ views: { 7: { key: "294-2", name: "Kalyx" } as CharacterView } });
    tab();
    expect(screen.getByText("live 7")).toBeTruthy();
  });

  it("says the character was forgotten once its file is gone", () => {
    usePlayed.setState({ played: [] });
    tab();
    expect(screen.getByText("This character was forgotten.")).toBeTruthy();
  });
});
