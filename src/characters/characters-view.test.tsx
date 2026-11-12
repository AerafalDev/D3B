import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import type { GameConnectionStatus, RelayStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { type CharacterView, useCharacters } from "./character-store.ts";

import { CharactersView } from "./characters-view.tsx";

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
    useCharacters.setState({ views: {}, selected: null });
    useRelay.setState({ status: null });
  });
  afterEach(cleanup);

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

  it("says when a connection's protocol is not read", () => {
    relay([connection(3, "unmapped")]);
    render(<CharactersView />);
    expect(screen.getByRole("button").textContent).toContain("Protocol not read");
  });

  it("chooses the character clicked, and marks it alone chosen", () => {
    relay([connection(1), connection(2)]);
    useCharacters.setState({ views: { 1: lyra } });
    render(<CharactersView />);
    const [first, second] = screen.getAllByRole("button");
    fireEvent.click(second ?? document.body);
    expect(useCharacters.getState().selected).toBe(2);
    expect(second?.getAttribute("aria-current")).toBe("true");
    expect(first?.hasAttribute("aria-current")).toBe(false);
  });
});
