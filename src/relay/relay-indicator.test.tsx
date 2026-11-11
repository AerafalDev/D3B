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

  it("says another system is not measured", () => {
    show({ ...ready, state: "unsupported", clientBuild: null });

    expect(screen.getByText("Relay not measured on this system")).toBeDefined();
  });

  it("shows nothing before the back end told its status", () => {
    show(null);

    expect(screen.queryByText(/Relay/)).toBeNull();
  });
});
