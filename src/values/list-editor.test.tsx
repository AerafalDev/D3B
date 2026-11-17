import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { SettingEditor } from "@/settings/setting-editor.tsx";

describe("a list's editor", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  afterEach(cleanup);

  const shown = (value: string | null) => {
    const onChange = vi.fn(async () => true);
    const durations = { valueType: "duration", list: true, bounds: null };
    render(<SettingEditor variable={durations} value={value} onChange={onChange} who="Lyra" label="Pauses" />);
    return onChange;
  };

  it("edits each value in its type's editor and sends the whole list", () => {
    const onChange = shown("[60, 90]");
    const second = screen.getByRole("textbox", { name: "Pauses, valeur 2" }) as HTMLInputElement;
    expect(second.value).toBe("1 min 30");
    fireEvent.change(second, { target: { value: "2 min" } });
    fireEvent.keyDown(second, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith("[60,120]");
  });

  it("removes a value and adds one typed in its last row", () => {
    const onChange = shown("[60, 90]");
    fireEvent.click(screen.getByRole("button", { name: "Retirer la valeur 1" }));
    expect(onChange).toHaveBeenLastCalledWith("[90]");
    const added = screen.getByRole("textbox", { name: "Pauses : ajouter une valeur" });
    fireEvent.change(added, { target: { value: "45 s" } });
    fireEvent.keyDown(added, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith("[60,90,45]");
  });

  it("starts empty when none is set, and adds nothing for text that holds no value", () => {
    const onChange = shown(null);
    expect(screen.queryByRole("button", { name: /Retirer/ })).toBeNull();
    const added = screen.getByRole("textbox", { name: "Pauses : ajouter une valeur" });
    fireEvent.change(added, { target: { value: "30" } });
    fireEvent.keyDown(added, { key: "Enter" });
    expect(onChange).not.toHaveBeenCalled();
    fireEvent.change(added, { target: { value: "30 s" } });
    fireEvent.keyDown(added, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith("[30]");
  });

  it("says ranges by their rows until their table comes", () => {
    const ranges = { valueType: "zone", ranges: true, bounds: null };
    const onChange = vi.fn(async () => true);
    const rows =
      '[{"from": 1, "to": 49, "value": {"sub_area": 445}}, {"from": 50, "to": 200, "value": {"sub_area": 1}}]';
    render(<SettingEditor variable={ranges} value={rows} onChange={onChange} who="Lyra" label="Où" />);
    expect(screen.getByText("2 tranches")).toBeTruthy();
  });
});
