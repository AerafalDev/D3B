import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { GraphNode } from "../node-sentence.ts";
import { OutputsEditor } from "./outputs-editor.tsx";

const out = (id: string) => ({ id, side: "out", carries: "exec", default: null });

const sequenceOf = (count: number) =>
  ({
    id: "seq",
    does: "kind",
    kind: "sequence",
    family: "flow",
    fromStep: null,
    pins: [{ ...out("run"), side: "in" }, ...Array.from({ length: count }, (_, at) => out(`then_${at + 1}`))],
    inputs: {},
    position: [0, 0],
    event: null,
    options: count === 2 ? {} : { count },
    adds: { pin: "then", side: "out", least: 2 },
  }) as unknown as GraphNode;

describe("a Sequence's outputs in Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("lists its outputs, each removed by its ×, and adds one", () => {
    const onEdit = vi.fn(async () => true);
    render(<OutputsEditor node={sequenceOf(3)} onEdit={onEdit} />);
    expect(screen.getByText("Alors 3")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Retirer Alors 2" }));
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "removePin", node: "seq", pin: "then_2" });
    cleanup();
    render(<OutputsEditor node={sequenceOf(3)} onEdit={onEdit} />);
    fireEvent.click(screen.getByRole("button", { name: "Ajouter une sortie" }));
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "options", node: "seq", count: 4 });
  });

  it("has no × at its kind's least", () => {
    render(<OutputsEditor node={sequenceOf(2)} onEdit={vi.fn(async () => true)} />);
    expect(screen.queryByRole("button", { name: /^Retirer/ })).toBeNull();
  });

  it("sends a removal once however quickly it is clicked, and takes the next after a refusal", async () => {
    const onEdit = vi.fn(async () => false);
    render(<OutputsEditor node={sequenceOf(3)} onEdit={onEdit} />);
    const remove = screen.getByRole("button", { name: "Retirer Alors 2" });
    fireEvent.click(remove);
    fireEvent.click(remove);
    expect(onEdit).toHaveBeenCalledTimes(1);
    await act(async () => {});
    fireEvent.click(remove);
    expect(onEdit).toHaveBeenCalledTimes(2);
  });
});
