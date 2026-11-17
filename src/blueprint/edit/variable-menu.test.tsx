import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { VariableMenu } from "./variable-menu.tsx";

describe("the menu of a variable dropped on the graph", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("reads or writes it, its first item focused, the arrows moving between both", () => {
    const onPick = vi.fn();
    render(<VariableMenu at={{ x: 10, y: 10 }} name="Objectif" onPick={onPick} onClose={() => {}} />);
    const [read, write] = screen.getAllByRole("menuitem");
    expect([read?.textContent, write?.textContent]).toEqual(["Lire Objectif", "Écrire Objectif"]);
    expect(document.activeElement).toBe(read);
    fireEvent.keyDown(screen.getByRole("menu"), { key: "ArrowDown" });
    expect(document.activeElement).toBe(write);
    if (write) {
      fireEvent.click(write);
    }
    expect(onPick).toHaveBeenCalledWith("set");
  });

  it("closes on Escape, or when the focus leaves it, without choosing", () => {
    const onPick = vi.fn();
    const onClose = vi.fn();
    render(<VariableMenu at={{ x: 10, y: 10 }} name="Objectif" onPick={onPick} onClose={onClose} />);
    fireEvent.keyDown(screen.getByRole("menu"), { key: "Escape" });
    fireEvent.blur(screen.getAllByRole("menuitem")[0] as HTMLElement, { relatedTarget: document.body });
    expect(onClose).toHaveBeenCalledTimes(2);
    expect(onPick).not.toHaveBeenCalled();
  });

  it("keeps its keys from the graph, and the focus on a mouse press", () => {
    const outside = vi.fn();
    render(
      // biome-ignore lint/a11y/noStaticElementInteractions: the graph's box, which listens to its keys
      <div onKeyDown={outside}>
        <VariableMenu at={{ x: 10, y: 10 }} name="Objectif" onPick={() => {}} onClose={() => {}} />
      </div>,
    );
    fireEvent.keyDown(screen.getByRole("menu"), { key: "Backspace" });
    expect(outside).not.toHaveBeenCalled();
    const pressed = fireEvent.mouseDown(screen.getAllByRole("menuitem")[1] as HTMLElement);
    expect(pressed, "a press keeps the focus where it is").toBe(false);
  });
});
