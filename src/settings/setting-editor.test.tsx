import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { SettingEditor } from "./setting-editor.tsx";

describe("a duration's editor", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });

  afterEach(cleanup);

  const shown = (value: string | null) => {
    const onChange = vi.fn(async () => true);
    const duration = { valueType: "duration", bounds: null };
    render(<SettingEditor variable={duration} value={value} onChange={onChange} who="Lyra" label="Durée" />);
    return { field: screen.getByRole("textbox", { name: "Durée" }) as HTMLInputElement, onChange };
  };

  it("shows the duration and sends what is typed as its seconds", () => {
    const { field, onChange } = shown("60");
    expect(field.value).toBe("1 min");
    fireEvent.change(field, { target: { value: "1 min 30" } });
    fireEvent.keyDown(field, { key: "Enter" });
    expect(onChange).toHaveBeenLastCalledWith("90");
  });

  it("marks text that holds no duration, sends nothing and puts the value back when left", () => {
    const { field, onChange } = shown("60");
    fireEvent.change(field, { target: { value: "30" } });
    expect(field.getAttribute("aria-invalid")).toBe("true");
    fireEvent.blur(field);
    expect(onChange).not.toHaveBeenCalled();
    expect(field.value).toBe("1 min");
  });

  it("puts the value back on Escape", () => {
    const { field, onChange } = shown("300");
    fireEvent.change(field, { target: { value: "2 min" } });
    fireEvent.keyDown(field, { key: "Escape" });
    expect(field.value).toBe("5 min");
    expect(onChange).not.toHaveBeenCalled();
  });
});
