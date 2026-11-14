import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import type { GameConnectionStatus, RelayStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { type CharacterView, useCharacters } from "./character-store.ts";
import { type PlayedCharacter, usePlayed } from "./played/played-store.ts";

// The editor area, faked: the editors opened, and the id of the one whose tab is active.
const opened = vi.hoisted(() => ({ editors: [] as unknown[], active: null as string | null }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: unknown) => opened.editors.push(editor),
  useEditors: (select: (store: { active: string | null }) => unknown) => select({ active: opened.active }),
}));

// The asset protocol's URL of a file, as Tauri makes it; happy-dom has no Tauri.
vi.mock("@tauri-apps/api/core", async (original) => ({
  ...(await original<typeof import("@tauri-apps/api/core")>()),
  convertFileSrc: (path: string) => `asset://${path}`,
}));

const { CharactersView, characterOf } = await import("./characters-view.tsx");

function connection(number: number, protocol: GameConnectionStatus["protocol"] = "on"): GameConnectionStatus {
  return {
    number,
    build: "3.7.13.17",
    protocol,
    understood: 0,
    read: 0,
    unknown: 0,
    notTrusted: 0,
    dropped: 0,
    map: null,
  };
}

function relay(connections: GameConnectionStatus[]) {
  const status: RelayStatus = {
    state: "ready",
    reason: null,
    clientBuild: null,
    gameConnections: connections.length,
    notUnderstood: 0,
    connections,
  };
  useRelay.setState({ status });
}

// Only what a row shows is set: the rest of the view plays no part in the list.
const lyra = {
  key: "294-1",
  name: "Lyra",
  level: 87,
  fighting: false,
  place: { mapId: 1, at: { x: 4, y: -18 }, subArea: { fr: "Forêt", en: "Forest" }, area: null },
} as CharacterView;

describe("the characters view", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    opened.editors = [];
    opened.active = null;
    useCharacters.setState({ views: {}, selected: null });
    useRelay.setState({ status: null });
    usePlayed.setState({ played: [] });
    // A fixed clock: how long ago each character was seen is read against it.
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(Date.UTC(2026, 10, 14, 12, 0));
  });
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("says how to start while no character plays", () => {
    render(<CharactersView />);
    expect(screen.getByText(/Start Dofus from Ankama's launcher/)).toBeTruthy();
  });

  it("lists a character by its name, level and place, and one before its name by its connection", () => {
    relay([connection(1), connection(2)]);
    useCharacters.setState({ views: { 1: lyra } });
    render(<CharactersView />);
    const rows = screen.getAllByRole("button").map((row) => row.textContent);
    expect(rows).toEqual(["LyLyra lvl 874, -18 · Forest", "CoConnection 2Entering the game…"]);
  });

  it("shows a character's breed head in place of its initials", () => {
    relay([connection(1), connection(2)]);
    useCharacters.setState({ views: { 1: { ...lyra, head: "data/heads/9-1.png" } } });
    const { container } = render(<CharactersView />);
    const images = [...container.querySelectorAll("img")].map((image) => image.getAttribute("src"));
    expect(images).toEqual(["asset://data/heads/9-1.png"]);
    const rows = screen.getAllByRole("button").map((row) => row.textContent);
    expect(rows).toEqual(["Lyra lvl 874, -18 · Forest", "CoConnection 2Entering the game…"]);
  });

  it("says when a connection's protocol is not read", () => {
    relay([connection(3, "unmapped")]);
    render(<CharactersView />);
    expect(screen.getByRole("button").textContent).toContain("Protocol not read");
  });

  it("marks the row whose character's tab is active, and it alone", () => {
    relay([connection(1), connection(2)]);
    opened.active = "character:2";
    render(<CharactersView />);
    const [first, second] = screen.getAllByRole("button");
    expect(second?.getAttribute("aria-current")).toBe("true");
    expect(first?.hasAttribute("aria-current")).toBe(false);
  });

  it("opens the character chosen in its tab, named after it", () => {
    relay([connection(1)]);
    useCharacters.setState({ views: { 1: lyra } });
    render(<CharactersView />);
    fireEvent.click(screen.getByRole("button"));
    expect(opened.editors).toEqual([
      { id: "character:1", component: "character", title: "Lyra", params: { icon: "user", connection: 1 } },
    ]);
  });

  it("tells the connection a character's editor shows, and none for another editor", () => {
    expect(characterOf("character:12")).toBe(12);
    expect(characterOf("character:x")).toBeNull();
    expect(characterOf("map:12")).toBeNull();
    expect(characterOf(null)).toBeNull();
  });

  function played(key: string, name: string, hoursAgo: number): PlayedCharacter {
    const lastSeen = Date.now() - hoursAgo * 3_600_000;
    return { key, name, level: 54, breedName: null, head: null, sex: null, lastSeen, place: null };
  }

  it("lists the characters played whose client is closed under the connected ones, greyed", () => {
    relay([connection(1)]);
    useCharacters.setState({ views: { 1: lyra } });
    usePlayed.setState({
      played: [played("294-1", "Lyra", 0), played("294-2", "Kalyx", 2), played("294-3", "Orrin", 48)],
    });
    render(<CharactersView />);
    const rows = screen.getAllByRole("button").map((row) => row.textContent?.replace(/[  ]/g, " "));
    // Lyra plays: she is listed once, connected.
    expect(rows).toEqual([
      "LyLyra lvl 874, -18 · Forest",
      "KaKalyx lvl 54Seen 2 hr. ago",
      "OrOrrin lvl 54Seen 2 days ago",
    ]);
    expect(screen.getAllByRole("separator", { hidden: true })).toHaveLength(1);
  });

  it("lists the characters played without a client open, and opens one in its own tab", () => {
    usePlayed.setState({ played: [played("294-2", "Kalyx", 0)] });
    render(<CharactersView />);
    expect(screen.queryByText(/Start Dofus/)).toBeNull();
    expect(screen.queryByRole("separator", { hidden: true })).toBeNull();
    fireEvent.click(screen.getByRole("button"));
    expect(opened.editors).toEqual([
      { id: "played:294-2", component: "played", title: "Kalyx", params: { icon: "user", key: "294-2" } },
    ]);
    expect(screen.getByRole("button").textContent).toContain("Seen just now");
  });

  it("keeps saying how long ago as the minutes pass", () => {
    vi.useFakeTimers();
    vi.setSystemTime(Date.UTC(2026, 10, 14, 12, 0));
    usePlayed.setState({ played: [played("294-2", "Kalyx", 0)] });
    render(<CharactersView />);
    expect(screen.getByRole("button").textContent).toContain("Seen just now");
    act(() => {
      vi.advanceTimersByTime(5 * 60_000);
    });
    expect(screen.getByRole("button").textContent?.replace(/[  ]/g, " ")).toContain("Seen 5 min. ago");
  });
});
