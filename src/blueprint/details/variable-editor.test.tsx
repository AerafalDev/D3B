import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { BlueprintSummary } from "@/library/library-store.ts";
import { VariableEditor } from "./variable-editor.tsx";

const pods: BlueprintSummary["variables"][number] = {
  id: "pods",
  name: "Banque à",
  valueType: "percentage",
  default: "90",
  bounds: [10, 100],
  list: false,
  ranges: false,
  perCharacter: true,
  memory: false,
  help: "",
};

function shown(variable = pods) {
  const onChange = vi.fn(() => Promise.resolve(true));
  const onDelete = vi.fn();
  render(<VariableEditor variable={variable} usedBy={2} onChange={onChange} onDelete={onDelete} />);
  return { onChange, onDelete };
}

function typed(label: string, text: string) {
  const field = screen.getByRole("textbox", { name: label });
  fireEvent.change(field, { target: { value: text } });
  fireEvent.blur(field);
  return field as HTMLInputElement;
}

describe("a variable of mine in Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("saves its name, default, bounds, help and switches whole, each when it is left", () => {
    const { onChange } = shown();
    typed("Nom", " Banque dès ");
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, name: "Banque dès" });
    typed("Valeur par défaut", "80");
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, default: "80" });
    typed("Plus petite valeur", "20");
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, bounds: [20, 100] });
    typed("Aide montrée au joueur", "Les pods qui envoient à la banque.");
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, help: "Les pods qui envoient à la banque." });
    fireEvent.click(screen.getByRole("switch", { name: "Réglage par personnage" }));
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, perCharacter: false });
    fireEvent.click(screen.getByRole("switch", { name: "Mémoire : gardée entre les séances" }));
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, memory: true });
    expect(screen.getByText("2 nœuds")).toBeTruthy();
  });

  it("puts back a blank name and bounds the wrong way round, saving nothing", () => {
    const { onChange } = shown();
    expect(typed("Nom", "   ").value).toBe("Banque à");
    typed("Plus grande valeur", "5");
    expect(onChange).not.toHaveBeenCalled();
    expect((screen.getByRole("textbox", { name: "Plus grande valeur" }) as HTMLInputElement).value).toBe("100");
    // A percentage's bounds stay within 0 to 100.
    typed("Plus grande valeur", "200");
    expect(onChange).not.toHaveBeenCalled();
  });

  it("saves both bounds once the focus leaves them, from none, and clears them both emptied", () => {
    const goal = { ...pods, id: "goal", valueType: "number", default: "5000", bounds: null };
    const { onChange } = shown(goal);
    const low = screen.getByRole("textbox", { name: "Plus petite valeur" });
    const high = screen.getByRole("textbox", { name: "Plus grande valeur" });
    fireEvent.change(low, { target: { value: "0" } });
    fireEvent.blur(low, { relatedTarget: high });
    expect(onChange, "the second is still to type").not.toHaveBeenCalled();
    fireEvent.change(high, { target: { value: "10000" } });
    fireEvent.blur(high, { relatedTarget: document.body });
    expect(onChange).toHaveBeenLastCalledWith({ ...goal, bounds: [0, 10000] });
    cleanup();
    const again = shown();
    const lowAgain = screen.getByRole("textbox", { name: "Plus petite valeur" });
    const highAgain = screen.getByRole("textbox", { name: "Plus grande valeur" });
    fireEvent.change(lowAgain, { target: { value: "" } });
    fireEvent.blur(lowAgain, { relatedTarget: highAgain });
    fireEvent.change(highAgain, { target: { value: "" } });
    fireEvent.blur(highAgain, { relatedTarget: null });
    expect(again.onChange).toHaveBeenLastCalledWith({ ...pods, bounds: null });
    cleanup();
    shown({ ...pods, valueType: "text", default: null, bounds: null });
    expect(screen.queryByRole("textbox", { name: "Plus petite valeur" })).toBeNull();
  });

  it("puts a field back when the back end refuses it", async () => {
    const onChange = vi.fn(() => Promise.resolve(false));
    render(<VariableEditor variable={pods} usedBy={0} onChange={onChange} onDelete={() => {}} />);
    const name = typed("Nom", "Ailleurs");
    const low = typed("Plus petite valeur", "20");
    await vi.waitFor(() => expect([name.value, low.value]).toEqual(["Banque à", "10"]));
  });

  it("sends a type chosen whole", async () => {
    const { onChange } = shown();
    fireEvent.click(screen.getByRole("combobox", { name: "Type" }));
    fireEvent.click(await screen.findByRole("option", { name: "Texte" }));
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, valueType: "text" });
  });

  it("deletes it", () => {
    const { onDelete } = shown();
    fireEvent.click(screen.getByRole("button", { name: "Supprimer la variable" }));
    expect(onDelete).toHaveBeenCalledOnce();
  });
});
