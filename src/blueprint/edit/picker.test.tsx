import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import type { ChoiceView_Serialize, PinView_Serialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { Picker } from "./picker.tsx";

const pin = (id: string, side: "in" | "out", carries: PinView_Serialize["carries"]): PinView_Serialize => ({
  id,
  side,
  carries,
  default: null,
});

const CHOICES: ChoiceView_Serialize[] = [
  {
    does: "kind",
    kind: "notify",
    family: "action",
    fromStep: null,
    pins: [pin("run", "in", "exec"), pin("text", "in", "text")],
  },
  {
    does: "kind",
    kind: "go_to_zone",
    family: "action",
    fromStep: 4,
    pins: [pin("run", "in", "exec"), pin("zone", "in", "zone")],
  },
  { does: "kind", kind: "finish", family: "flow", fromStep: null, pins: [pin("run", "in", "exec")] },
  { does: "get", variable: "zone", family: "variable", fromStep: null, pins: [pin("value", "out", "zone")] },
];

const NAMES = new Map([["zone", "Zone de récolte"]]);
const FAILED = { node: "n3", pin: pin("failed", "out", "exec") };

describe("the picker", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("says each choice by its sentence and description, by family, an action not done yet with its step", () => {
    render(
      <Picker at={{ x: 0, y: 0 }} choices={CHOICES} dragged={null} names={NAMES} onPick={vi.fn()} onClose={vi.fn()} />,
    );
    expect(screen.getByText("Actions")).toBeTruthy();
    expect(screen.getByText("Zone de récolte")).toBeTruthy();
    expect(screen.getByText("étape 4")).toBeTruthy();
    expect(screen.queryByText("Souvent ici")).toBeNull();
  });

  it("puts first what often follows a Failed and leaves out what the wire does not fit", () => {
    render(
      <Picker
        at={{ x: 0, y: 0 }}
        choices={CHOICES}
        dragged={FAILED}
        names={NAMES}
        onPick={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByText("Souvent ici")).toBeTruthy();
    expect(screen.queryByText("Zone de récolte")).toBeNull();
  });

  it("chooses by the arrows and Enter, and closes on Escape", () => {
    const onPick = vi.fn();
    const onClose = vi.fn();
    render(
      <Picker at={{ x: 0, y: 0 }} choices={CHOICES} dragged={FAILED} names={NAMES} onPick={onPick} onClose={onClose} />,
    );
    const search = screen.getByRole("combobox");
    fireEvent.keyDown(search, { key: "ArrowDown" });
    fireEvent.keyDown(search, { key: "Enter" });
    // Often here: notify, finish; then all: notify, go to a zone, finish.
    expect(onPick).toHaveBeenCalledWith(CHOICES[2]);
    fireEvent.keyDown(search, { key: "Escape" });
    expect(onClose).toHaveBeenCalled();
  });

  it("says when nothing fits the search", () => {
    render(
      <Picker at={{ x: 0, y: 0 }} choices={CHOICES} dragged={null} names={NAMES} onPick={vi.fn()} onClose={vi.fn()} />,
    );
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "téléporter" } });
    expect(screen.getByText("Rien ne convient ici.")).toBeTruthy();
  });
});
