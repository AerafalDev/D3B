import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { initI18n } from "@/i18n/i18n.ts";
import { icons } from "@/icons/icons.ts";
import { useRelay } from "@/relay/relay-store.ts";
import { UpdateItem } from "./update-item.tsx";
import { useUpdater } from "./updater.ts";

const restart = vi.fn(async () => {});

function renderItem() {
  return render(
    <TooltipProvider>
      <UpdateItem />
    </TooltipProvider>,
  );
}

function withConnections(gameConnections: number) {
  useRelay.setState({
    status: { state: "ready", reason: null, clientBuild: null, gameConnections, notUnderstood: 0, connections: [] },
  });
}

describe("the update's item", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    restart.mockClear();
    useUpdater.setState({ state: { status: "idle" }, restart });
    useRelay.setState({ status: null });
  });
  afterEach(cleanup);

  it("shows nothing while no update is downloaded", () => {
    useUpdater.setState({ state: { status: "downloading", version: "0.14.0" } });
    const { container } = renderItem();
    expect(container.textContent).toBe("");
  });

  it("offers to restart into the downloaded version, with JetBrains' update icon, and restarts once chosen", () => {
    useUpdater.setState({ state: { status: "ready", version: "0.14.0", notes: undefined } });
    const { container } = renderItem();
    expect([...container.querySelectorAll("img")].map((image) => image.getAttribute("src"))).toEqual([icons.ideUpdate]);
    fireEvent.click(screen.getByRole("button", { name: "Restart to update to 0.14.0" }));
    expect(restart).toHaveBeenCalledTimes(1);
  });

  it("warns in its hover that restarting ends the game connections, while some are open", async () => {
    useUpdater.setState({ state: { status: "ready", version: "0.14.0", notes: undefined } });
    withConnections(2);
    renderItem();
    fireEvent.focus(screen.getByRole("button", { name: "Restart to update to 0.14.0" }));
    expect((await screen.findAllByText("Restarting ends the game connections D3B relays now.")).length).toBeGreaterThan(
      0,
    );
  });

  it("shows the release's notes in its hover, at most six lines", async () => {
    const notes = ["", "One", "Two", "Three", "Four", "Five", "Six", "Seven"].join("\n");
    useUpdater.setState({ state: { status: "ready", version: "0.14.0", notes } });
    renderItem();
    fireEvent.focus(screen.getByRole("button", { name: "Restart to update to 0.14.0" }));
    expect((await screen.findAllByText("Six")).length).toBeGreaterThan(0);
    expect(screen.queryByText("Seven")).toBeNull();
  });

  it("opens no hover when it has nothing to add", () => {
    useUpdater.setState({ state: { status: "ready", version: "0.14.0", notes: "  " } });
    withConnections(0);
    renderItem();
    fireEvent.focus(screen.getByRole("button", { name: "Restart to update to 0.14.0" }));
    expect(screen.queryByRole("tooltip")).toBeNull();
  });

  it("says it installs, and cannot be chosen again meanwhile, still focusable so that it reads", () => {
    useUpdater.setState({ state: { status: "installing", version: "0.14.0" } });
    renderItem();
    const item = screen.getByRole("button", { name: "Installing 0.14.0…" });
    expect(item.getAttribute("aria-disabled")).toBe("true");
    expect(item.hasAttribute("disabled")).toBe(false);
    fireEvent.click(item);
    expect(restart).not.toHaveBeenCalled();
  });

  it("keeps the keyboard's focus once chosen, as its label turns to the install", () => {
    useUpdater.setState({ state: { status: "ready", version: "0.14.0", notes: undefined } });
    withConnections(1);
    restart.mockImplementationOnce(async () => {
      useUpdater.setState({ state: { status: "installing", version: "0.14.0" } });
    });
    renderItem();
    const item = screen.getByRole("button", { name: "Restart to update to 0.14.0" });
    item.focus();
    fireEvent.click(item);
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Installing 0.14.0…" }));
  });

  it("shows a line the notes repeat once", async () => {
    useUpdater.setState({ state: { status: "ready", version: "0.14.0", notes: "Fixes\nFixes\nMore" } });
    renderItem();
    fireEvent.focus(screen.getByRole("button", { name: "Restart to update to 0.14.0" }));
    await screen.findAllByText("More");
    expect(screen.getAllByText("Fixes")).toHaveLength(screen.getAllByText("More").length);
  });
});
