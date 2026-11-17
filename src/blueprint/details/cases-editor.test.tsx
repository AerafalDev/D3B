import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { GraphNode } from "../node-sentence.ts";
import { CasesEditor } from "./cases-editor.tsx";

const switchOf = (options: object) =>
  ({
    id: "sw",
    does: "kind",
    kind: "switch",
    family: "flow",
    fromStep: null,
    pins: [],
    inputs: {},
    position: [0, 0],
    event: null,
    takesCases: true,
    options,
  }) as unknown as GraphNode;

function shown(node: GraphNode, onEdit = vi.fn(async () => true)) {
  render(<CasesEditor node={node} types={["number", "duration"]} who="Selon" onEdit={onEdit} />);
  return onEdit;
}

function typed(name: string, text: string) {
  const field = screen.getByRole("textbox", { name });
  fireEvent.change(field, { target: { value: text } });
  fireEvent.keyDown(field, { key: "Enter" });
}

describe("a Switch's cases in Details", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("edits a case in its type's editor, sending the whole cases", () => {
    const onEdit = shown(switchOf({ valueType: "number", cases: ["24", "28"] }));
    typed("Cas 2", "30");
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "options", node: "sw", cases: ["24", "30"], of: "number" });
  });

  it("removes a case by its output, and adds one from the last row", () => {
    const onEdit = shown(switchOf({ valueType: "number", cases: ["24", "28"] }));
    fireEvent.click(screen.getByRole("button", { name: "Retirer le cas 1" }));
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "removePin", node: "sw", pin: "case_1" });
    typed("Ajouter un cas", "5");
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "options", node: "sw", cases: ["28", "5"], of: "number" });
  });

  it("builds on the cases as they were after a change refused", async () => {
    const onEdit = shown(
      switchOf({ valueType: "number", cases: ["24"] }),
      vi.fn(async () => false),
    );
    typed("Cas 1", "25");
    await act(async () => {});
    typed("Ajouter un cas", "5");
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "options", node: "sw", cases: ["24", "5"], of: "number" });
  });

  it("marks the case given again, not the first", () => {
    const { container } = render(
      <CasesEditor
        node={switchOf({ valueType: "number", cases: ["24", "28", "24"] })}
        types={["number"]}
        who="Selon"
        onEdit={vi.fn(async () => true)}
      />,
    );
    const marked = [...container.querySelectorAll("p")].map((said) =>
      said.parentElement?.querySelector("input")?.getAttribute("aria-label"),
    );
    expect(marked).toEqual(["Cas 3"]);
  });

  it("removes a case once however quickly × is clicked, and a removal refused is put back", async () => {
    const onEdit = shown(
      switchOf({ valueType: "number", cases: ["24", "28", "30"] }),
      vi.fn(async () => false),
    );
    const remove = screen.getByRole("button", { name: "Retirer le cas 2" });
    fireEvent.click(remove);
    fireEvent.click(remove);
    expect(onEdit).toHaveBeenCalledTimes(1);
    await act(async () => {});
    typed("Ajouter un cas", "5");
    expect(onEdit).toHaveBeenLastCalledWith({
      edit: "options",
      node: "sw",
      cases: ["24", "28", "30", "5"],
      of: "number",
    });
  });

  it("has the cases' type chosen while the Switch notes none, and gives it with the first case", async () => {
    const onEdit = shown(switchOf({}));
    expect(screen.getByRole("combobox", { name: "Type des cas" })).toBeTruthy();
    typed("Ajouter un cas", "5");
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "options", node: "sw", cases: ["5"], of: "number" });
    fireEvent.click(screen.getByRole("combobox", { name: "Type des cas" }));
    fireEvent.click(await screen.findByRole("option", { name: "Durée" }));
    typed("Ajouter un cas", "1 min 30");
    expect(onEdit).toHaveBeenLastCalledWith({ edit: "options", node: "sw", cases: ["5", "90"], of: "duration" });
    cleanup();
    shown(switchOf({ valueType: "number", cases: ["5"] }));
    expect(screen.queryByRole("combobox", { name: "Type des cas" })).toBeNull();
  });
});
