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
  });

  it("clears its bounds when both are emptied, and offers none to a text", () => {
    const { onChange } = shown();
    const low = screen.getByRole("textbox", { name: "Plus petite valeur" });
    fireEvent.change(low, { target: { value: "" } });
    typed("Plus grande valeur", "");
    expect(onChange).toHaveBeenLastCalledWith({ ...pods, bounds: null });
    cleanup();
    shown({ ...pods, valueType: "text", default: null, bounds: null });
    expect(screen.queryByRole("textbox", { name: "Plus petite valeur" })).toBeNull();
  });

  it("deletes it", () => {
    const { onDelete } = shown();
    fireEvent.click(screen.getByRole("button", { name: "Supprimer la variable" }));
    expect(onDelete).toHaveBeenCalledOnce();
  });
});
