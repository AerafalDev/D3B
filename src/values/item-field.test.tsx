import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end, faked: the items it finds by name, and those it gives back by id.
const backEnd = vi.hoisted(() => ({ findItems: vi.fn() }));
vi.mock("@/bindings.ts", () => ({ commands: { findItems: backEnd.findItems } }));
vi.mock("@tauri-apps/api/core", () => ({ convertFileSrc: (path: string) => `asset://${path}` }));

const { ItemField } = await import("./item-field.tsx");

const wheat = {
  id: 289,
  name: { fr: "Blé", en: "Wheat" },
  level: 1,
  kind: { fr: "Céréale", en: "Cereal" },
  icon: "data/icons/42011.png",
};
const seed = { ...wheat, id: 400, name: { fr: "Graine de blé", en: "Wheat seed" }, level: 5, icon: null };

describe("an item's field", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  beforeEach(() => {
    vi.useFakeTimers();
    backEnd.findItems.mockImplementation(async (query: string, ids: number[]) =>
      ids.length > 0 ? [wheat, seed].filter((item) => ids.includes(item.id)) : query === "ble" ? [wheat, seed] : [],
    );
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    backEnd.findItems.mockReset();
  });

  const searched = async (query: string) => {
    const onChange = vi.fn(async () => true);
    render(<ItemField value={null} onChange={onChange} label="Objet" />);
    fireEvent.change(screen.getByRole("searchbox", { name: "Objet : chercher un objet" }), {
      target: { value: query },
    });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    return onChange;
  };

  it("finds items by name with their icon, type and level, and a click chooses one", async () => {
    const onChange = await searched("ble");
    expect(screen.getByText("Céréale · niv. 5")).toBeTruthy();
    expect(document.querySelectorAll("img")).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: /Graine de blé/ }));
    expect(onChange).toHaveBeenLastCalledWith("400");
  });

  it("chooses the first found by Enter, and nothing when none is found", async () => {
    const onChange = await searched("ble");
    fireEvent.keyDown(screen.getByRole("searchbox"), { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith("289");
    cleanup();
    const none = await searched("épée");
    fireEvent.keyDown(screen.getByRole("searchbox"), { key: "Enter" });
    expect(none).not.toHaveBeenCalled();
  });

  it("asks nothing within the pause, and Enter never takes an earlier search's first", async () => {
    const onChange = await searched("ble");
    const box = screen.getByRole("searchbox");
    backEnd.findItems.mockClear();
    fireEvent.change(box, { target: { value: "bois" } });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });
    expect(backEnd.findItems).not.toHaveBeenCalled();
    fireEvent.keyDown(box, { key: "Enter" });
    expect(onChange).not.toHaveBeenCalled();
  });

  it("clears the search on Escape and says when nothing is found", async () => {
    await searched("épée");
    expect(screen.getByText("Aucun objet trouvé")).toBeTruthy();
    const box = screen.getByRole("searchbox") as HTMLInputElement;
    fireEvent.keyDown(box, { key: "Escape" });
    expect(box.value).toBe("");
  });

  it("shows the item saved before when a choice is refused", async () => {
    const onChange = vi.fn(async () => false);
    render(<ItemField value="289" onChange={onChange} label="Objet" />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "ble" } });
    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });
    fireEvent.click(screen.getByRole("button", { name: /Graine de blé/ }));
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(screen.getByText("Blé · niv. 1")).toBeTruthy();
  });

  it("shows the item chosen by its name and level, else by its id without the data", async () => {
    render(<ItemField value="289" onChange={vi.fn()} label="Objet" />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(screen.getByText("Blé · niv. 1")).toBeTruthy();
    cleanup();
    backEnd.findItems.mockRejectedValue({ kind: "noData", reason: "no data" });
    render(<ItemField value="289" onChange={vi.fn()} label="Objet" />);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    expect(screen.getByText("Objet 289")).toBeTruthy();
  });
});
