import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import type { RelayStatus } from "@/bindings.ts";
import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { icons } from "@/icons/icons.ts";
import { RelayItem } from "./relay-item.tsx";
import { useRelay } from "./relay-store.ts";

const ready: RelayStatus = {
  state: "ready",
  reason: null,
  clientBuild: "3.7.13.17",
  gameConnections: 1,
  notUnderstood: 0,
  connections: [],
};

function renderItem() {
  return render(
    <TooltipProvider>
      <RelayItem dataBuild="3.7.13.17" />
    </TooltipProvider>,
  );
}

function shownIcons(container: HTMLElement) {
  return [...container.querySelectorAll("img")].map((image) => image.getAttribute("src"));
}

describe("the relay's item", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => useRelay.setState({ status: null }));
  afterEach(cleanup);

  it("shows nothing until the back end tells the relay's status, as in the browser preview", () => {
    const { container } = renderItem();
    expect(container.textContent).toBe("");
  });

  it("shows the state and the game connections, grey, with the relay's arrows", () => {
    useRelay.setState({ status: ready });
    const { container } = renderItem();
    const label = screen.getByText("Relay ready · 1 in game");
    expect(label.closest(".text-error")).toBeNull();
    expect(shownIcons(container)).toEqual([icons.arrowLeftRight]);
  });

  it("shows a relay off in red, with JetBrains' error", () => {
    useRelay.setState({ status: { ...ready, state: "failed", reason: "port 5555 is taken", gameConnections: 0 } });
    const { container } = renderItem();
    expect(screen.getByText("Relay off").closest(".text-error")).not.toBeNull();
    expect(shownIcons(container)).toEqual([icons.error]);
  });

  it("opens its hover from the keyboard", async () => {
    useRelay.setState({ status: ready });
    renderItem();
    const item = screen.getByText("Relay ready · 1 in game").closest("[tabindex]");
    if (item === null) {
      throw new Error("the item takes no focus");
    }
    fireEvent.focus(item);
    expect((await screen.findAllByText("Quitting D3B ends these game connections.")).length).toBeGreaterThan(0);
  });
});
