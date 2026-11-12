import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import type { RelayStatus } from "@/bindings.ts";
import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { RelayIndicator } from "./relay-indicator.tsx";
import { useRelay } from "./relay-store.ts";

const ready: RelayStatus = {
  state: "ready",
  reason: null,
  clientBuild: "3.7.13.17",
  gameConnections: 0,
  notUnderstood: 0,
  connections: [],
};

function show(status: RelayStatus | null) {
  useRelay.setState({ status, follow: () => () => {} });
  render(
    <TooltipProvider delayDuration={0}>
      <RelayIndicator />
    </TooltipProvider>,
  );
}

/** Opens the tooltip from the keyboard and returns its text. */
async function tooltip(label: string): Promise<string> {
  fireEvent.focus(screen.getByText(label));
  return (await screen.findAllByRole("tooltip")).map((element) => element.textContent).join(" ");
}

describe("RelayIndicator", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  afterEach(cleanup);

  it("says the relay is ready with its game connections, and names the client", async () => {
    show({ ...ready, gameConnections: 2 });

    expect(screen.getByText("Relay ready · 2 in game")).toBeDefined();
    const text = await tooltip("Relay ready · 2 in game");
    expect(text).toContain("Client 3.7.13.17");
    expect(text).toContain("Quitting D3B ends these game connections.");
    expect(text).toContain("Start Dofus after D3B");
  });

  it("gives the reason it failed", async () => {
    show({ ...ready, state: "failed", reason: "port 5555 of 127.0.13.59 cannot be taken" });

    expect(await tooltip("Relay off")).toContain("port 5555 of 127.0.13.59 cannot be taken");
  });

  it("counts the connections that went straight to Ankama", async () => {
    show({ ...ready, notUnderstood: 3 });

    expect(await tooltip("Relay ready · 0 in game")).toContain("3 connections went straight to Ankama");
  });

  it("says one connection in the singular, and that quitting ends nothing when none is open", async () => {
    show({ ...ready, notUnderstood: 1 });

    const text = await tooltip("Relay ready · 0 in game");
    expect(text).toContain("1 connection went straight to Ankama: its server choice");
    expect(text).not.toContain("Quitting D3B");
  });

  it("shows a game connection read on two lines: who it is with its map, then its counts", async () => {
    show({
      ...ready,
      gameConnections: 1,
      connections: [
        {
          number: 1,
          build: "3.7.13.17",
          protocol: "on",
          understood: 61204,
          read: 55012,
          unknown: 512330,
          notTrusted: 0,
          dropped: 3,
          map: { x: 4, y: -18 },
        },
      ],
    });

    const text = await tooltip("Relay ready · 1 in game");
    expect(text).toContain("Connection 1 — 3.7.13.17, protocol on, map 4,-18");
    expect(text).toContain("understood 61,204 · read 55,012 · unknown 512,330 · not trusted 0 · dropped 3");
  });

  it("shows a connection lost with its counts, and one read without a map yet without one", async () => {
    const counts = { understood: 5, read: 0, unknown: 7, notTrusted: 0, dropped: 0 };
    show({
      ...ready,
      gameConnections: 2,
      connections: [
        { ...counts, number: 1, build: "3.7.13.17", protocol: "lost", map: null },
        { ...counts, number: 2, build: "3.7.13.17", protocol: "on", map: null },
      ],
    });

    const lines = (await tooltip("Relay ready · 2 in game")).split(/(?=Connection|understood|Quitting|Start)/);
    expect(lines).toContain("Connection 1 — 3.7.13.17, protocol lost (unreadable frames)");
    expect(lines).toContain("Connection 2 — 3.7.13.17, protocol on");
    expect(lines.filter((line) => line.startsWith("understood 5"))).toHaveLength(2);
    const indented = (await screen.findAllByText(/^understood 5/)).filter((line) => line.classList.contains("pl-3"));
    expect(indented.length).toBeGreaterThanOrEqual(2);
  });

  it("shows a game connection not read on one line, with why", async () => {
    const off = { understood: 0, read: 0, unknown: 0, notTrusted: 0, dropped: 0, map: null };
    show({
      ...ready,
      gameConnections: 2,
      connections: [
        { ...off, number: 2, build: "3.7.14.1", protocol: "unmapped" },
        { ...off, number: 3, build: null, protocol: "unknownBuild" },
      ],
    });

    const text = await tooltip("Relay ready · 2 in game");
    expect(text).toContain("Connection 2 — 3.7.14.1, protocol off (build without pins)");
    expect(text).toContain("Connection 3 — protocol off (unknown build)");
    expect(text).not.toContain("understood");
  });

  it("says another system is not measured", () => {
    show({ ...ready, state: "unsupported", clientBuild: null });

    expect(screen.getByText("Relay not measured on this system")).toBeDefined();
  });

  it("shows nothing before the back end told its status", () => {
    show(null);

    expect(screen.queryByText(/Relay/)).toBeNull();
  });
});
