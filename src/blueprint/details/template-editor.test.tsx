import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { GraphNode } from "../node-sentence.ts";
import { TemplateEditor } from "./template-editor.tsx";

const text = (template?: string) =>
  ({
    id: "tx",
    does: "kind",
    kind: "text",
    family: "value",
    fromStep: null,
    pins: [],
    inputs: {},
    position: [0, 0],
    event: null,
    takesText: true,
    options: template === undefined ? {} : { text: template },
  }) as unknown as GraphNode;

describe("a Text's template in Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("saves the template once the field is left, and not when it is unchanged", () => {
    const onEdit = vi.fn(async () => true);
    render(<TemplateEditor node={text("{n} récoltes")} onEdit={onEdit} />);
    const field = screen.getByRole("textbox", { name: "Modèle du texte" }) as HTMLInputElement;
    expect(field.value).toBe("{n} récoltes");
    fireEvent.blur(field);
    expect(onEdit).not.toHaveBeenCalled();
    fireEvent.change(field, { target: { value: "{n} en {temps}" } });
    fireEvent.keyDown(field, { key: "Enter" });
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "text", node: "tx", text: "{n} en {temps}" });
  });

  it("puts the template back when it is refused", async () => {
    render(<TemplateEditor node={text()} onEdit={vi.fn(async () => false)} />);
    const field = screen.getByRole("textbox", { name: "Modèle du texte" }) as HTMLInputElement;
    fireEvent.change(field, { target: { value: "{x}" } });
    await act(async () => {
      fireEvent.blur(field);
    });
    expect(field.value).toBe("");
  });
});
