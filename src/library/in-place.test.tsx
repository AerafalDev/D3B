import { cleanup, fireEvent, render, screen } from "@testing-library/react";
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
    fireEvent.keyDown(field, { key: "Enter" });
    fireEvent.blur(field);
    expect(onSave).toHaveBeenCalledWith("Mes blés");
    expect(screen.queryByRole("textbox")).toBeNull();
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
});
