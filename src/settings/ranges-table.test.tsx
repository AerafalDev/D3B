import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { SettingEditor } from "./setting-editor.tsx";

const durations = { valueType: "duration", ranges: true, bounds: null };
const rows = (...made: [number, number, number][]) =>
  JSON.stringify(made.map(([from, to, value]) => ({ from, to, value })));

describe("a ranges' table", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  afterEach(cleanup);

  const shown = (value: string | null) => {
    const onChange = vi.fn(async () => true);
    render(<SettingEditor variable={durations} value={value} onChange={onChange} who="Lyra" label="Pause" />);
    return onChange;
  };

  it("lists its rows sorted, each value in its type's editor, and the levels no row reaches", () => {
    shown(rows([50, 59, 120], [1, 9, 60]));
    expect((screen.getByRole("textbox", { name: "Pause, tranche 1" }) as HTMLInputElement).value).toBe("1 min");
    expect((screen.getByRole("textbox", { name: "Pause, tranche 2" }) as HTMLInputElement).value).toBe("2 min");
    expect(screen.getByText("Niveaux 10 à 49 : rien")).toBeTruthy();
    expect(screen.getByText("Niveaux 60 à 200 : rien")).toBeTruthy();
  });

  it("marks two rows a level reaches at once", () => {
    shown(rows([1, 50, 60], [50, 80, 90]));
    expect(screen.getByText("Deux tranches partagent un niveau.")).toBeTruthy();
    expect(screen.getByRole("textbox", { name: "Tranche 1 : au niveau" }).getAttribute("aria-invalid")).toBe("true");
  });

  it("changes a row's level and removes a row, sending the whole ranges", () => {
    const onChange = shown(rows([1, 49, 60], [50, 200, 120]));
    const to = screen.getByRole("textbox", { name: "Tranche 1 : au niveau" });
    fireEvent.change(to, { target: { value: "39" } });
    fireEvent.blur(to);
    expect(onChange).toHaveBeenLastCalledWith(rows([1, 39, 60], [50, 200, 120]));
    fireEvent.click(screen.getByRole("button", { name: "Retirer la tranche 2" }));
    expect(onChange).toHaveBeenLastCalledWith(rows([1, 39, 60]));
  });

  it("removes the row whose level was just changed, though the ranges did not come back yet", () => {
    const onChange = shown(rows([1, 49, 60], [50, 200, 120]));
    const to = screen.getByRole("textbox", { name: "Tranche 1 : au niveau" });
    fireEvent.change(to, { target: { value: "39" } });
    fireEvent.blur(to);
    fireEvent.click(screen.getByRole("button", { name: "Retirer la tranche 1" }));
    expect(onChange).toHaveBeenLastCalledWith(rows([50, 200, 120]));
  });

  it("edits a row's value, and adds a row from the level after the highest", () => {
    const onChange = shown(rows([50, 59, 60], [1, 9, 60]));
    const second = screen.getByRole("textbox", { name: "Pause, tranche 2" });
    fireEvent.change(second, { target: { value: "3 min" } });
    fireEvent.keyDown(second, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith(rows([50, 59, 180], [1, 9, 60]));
    expect(screen.getByText("Niveaux 60 à 200 :")).toBeTruthy();
    const added = screen.getByRole("textbox", { name: "Pause : ajouter une tranche" });
    fireEvent.change(added, { target: { value: "45 s" } });
    fireEvent.keyDown(added, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith(rows([50, 59, 180], [1, 9, 60], [60, 200, 45]));
  });

  it("offers no row to add once a row reaches level 200", () => {
    shown(rows([1, 49, 60], [50, 200, 120]));
    expect(screen.queryByRole("textbox", { name: "Pause : ajouter une tranche" })).toBeNull();
  });

  it("builds on the ranges as they were after a change refused", async () => {
    const onChange = vi.fn(async () => false);
    render(
      <SettingEditor variable={durations} value={rows([1, 200, 60])} onChange={onChange} who="Lyra" label="Pause" />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Retirer la tranche 1" }));
    await act(async () => {});
    const value = screen.getByRole("textbox", { name: "Pause, tranche 1" });
    fireEvent.change(value, { target: { value: "2 min" } });
    fireEvent.keyDown(value, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith(rows([1, 200, 120]));
  });

  it("says a pasted table refused, keeping the text", async () => {
    const onChange = vi.fn(async () => false);
    render(<SettingEditor variable={durations} value={null} onChange={onChange} who="Lyra" label="Pause" />);
    const text = screen.getByRole("textbox", { name: "Pause : tranches en texte" }) as HTMLTextAreaElement;
    fireEvent.change(text, { target: { value: "1-200: 1 min" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Remplacer le tableau" }));
    });
    expect(screen.getByText("Le tableau n'a pas été gardé : une valeur ne tient pas.")).toBeTruthy();
    expect(text.value).toBe("1-200: 1 min");
  });

  it("puts back a level out of 1 to 200 or past its other end", () => {
    const onChange = shown(rows([10, 49, 60]));
    const from = screen.getByRole("textbox", { name: "Tranche 1 : du niveau" }) as HTMLInputElement;
    for (const wrong of ["0", "50", "dix"]) {
      fireEvent.change(from, { target: { value: wrong } });
      fireEvent.blur(from);
      expect(from.value).toBe("10");
    }
    expect(onChange).not.toHaveBeenCalled();
  });

  it("replaces the table with a text pasted, and says a line it cannot read, changing nothing", async () => {
    const onChange = shown(rows([1, 200, 60]));
    const text = screen.getByRole("textbox", { name: "Pause : tranches en texte" });
    fireEvent.change(text, { target: { value: "1-49: 1 min 30\n50-x: 2 min" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Remplacer le tableau" }));
    });
    expect(screen.getByText("La ligne 2 ne se lit pas : rien n'a changé.")).toBeTruthy();
    fireEvent.change(text, { target: { value: "1-49: 1 min\n50-200: min" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Remplacer le tableau" }));
    });
    expect(screen.getByText("La ligne 2 ne se lit pas : rien n'a changé.")).toBeTruthy();
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.change(text, { target: { value: "1-49: 1 min 30\n50\t200\t2 min" } });
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Remplacer le tableau" }));
    });
    expect(onChange).toHaveBeenLastCalledWith(rows([1, 49, 90], [50, 200, 120]));
  });

  it("copies the table as text that reads back the same", async () => {
    shown(rows([1, 49, 90], [50, 200, 120]));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Copier en texte" }));
    });
    const text = screen.getByRole("textbox", { name: "Pause : tranches en texte" }) as HTMLTextAreaElement;
    expect(text.value).toBe("1-49: 1 min 30\n50-200: 2 min");
  });
});
