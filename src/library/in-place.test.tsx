import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { InPlace } from "./in-place.tsx";

function shown(blank: boolean, value = "Ma récolte") {
  const onSave = vi.fn(() => Promise.resolve(true));
  render(<InPlace value={value} label="Nom" blank={blank} placeholder="Ajouter" onSave={onSave} />);
  return onSave;
}

describe("a text edited in place", () => {
  afterEach(cleanup);

  it("turns into a field on a click and saves it trimmed on Enter", () => {
    const onSave = shown(false);
    fireEvent.click(screen.getByRole("button", { name: "Ma récolte" }));
    const field = screen.getByRole("textbox", { name: "Nom" });
    fireEvent.change(field, { target: { value: " Mes blés " } });
    act(() => {
      fireEvent.keyDown(field, { key: "Enter" });
    });
    expect(onSave).toHaveBeenCalledWith("Mes blés");
    expect(screen.queryByRole("textbox")).toBeNull();
    expect(document.activeElement, "the text takes the focus back").toBe(screen.getByRole("button"));
  });

  it("leaves it on Escape, on the same text, and on a blank name, but saves a blank description", () => {
    const onSave = shown(false);
    const edit = (text: string, key?: string) => {
      fireEvent.click(screen.getByRole("button"));
      const field = screen.getByRole("textbox");
      fireEvent.change(field, { target: { value: text } });
      if (key !== undefined) {
        fireEvent.keyDown(field, { key });
      }
      fireEvent.blur(field);
    };
    edit("Ailleurs", "Escape");
    edit("Ma récolte");
    edit("   ");
    expect(onSave).not.toHaveBeenCalled();
    cleanup();
    const described = shown(true, "Le blé.");
    edit("");
    expect(described).toHaveBeenCalledWith("");
  });

  it("says its placeholder when empty", () => {
    shown(true, "");
    expect(screen.getByRole("button").textContent).toBe("Ajouter");
  });

  it("keeps a description's line breaks, Shift+Enter typing one and its hover saying it whole", () => {
    const onSave = vi.fn(() => Promise.resolve(true));
    render(<InPlace value={"Le blé.\nPuis l'orge."} label="Description" blank multiline onSave={onSave} />);
    expect(screen.getByRole("button").getAttribute("title")).toBe("Le blé.\nPuis l'orge.");
    fireEvent.click(screen.getByRole("button"));
    const field = screen.getByRole("textbox", { name: "Description" });
    expect(field.tagName).toBe("TEXTAREA");
    fireEvent.keyDown(field, { key: "Enter", shiftKey: true });
    expect(screen.getByRole("textbox"), "Shift+Enter leaves it open").toBe(field);
    fireEvent.change(field, { target: { value: "Le blé.\nPuis l'avoine." } });
    act(() => {
      fireEvent.keyDown(field, { key: "Enter" });
    });
    expect(onSave).toHaveBeenCalledWith("Le blé.\nPuis l'avoine.");
  });
});
