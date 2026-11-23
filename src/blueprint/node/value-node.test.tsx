import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ReactFlowProvider } from "@xyflow/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { NodeEdit } from "../edit/node-options.ts";
import type { GraphNode } from "../node-sentence.ts";
import { ValueNode } from "./value-node.tsx";

const pin = (id: string, side: "in" | "out", carries: string, fallback: string | null = null) =>
  ({ id, side, carries, default: fallback }) as GraphNode["pins"][number];

const value = (kind: string, pins: GraphNode["pins"], more: object = {}) =>
  ({
    id: kind,
    does: "kind",
    kind,
    family: "value",
    fromStep: null,
    pins,
    inputs: {},
    position: [0, 0],
    event: null,
    ...more,
  }) as unknown as GraphNode;

function drawn(node: GraphNode, wired: string[] = [], onEdit?: (edit: GraphEdit_Deserialize) => void) {
  const props = {
    data: { node, sentence: [{ text: "Pods % < 80" }], wired },
    selected: false,
  } as unknown as Parameters<typeof ValueNode>[0];
  return render(
    <ReactFlowProvider>
      <NodeEdit value={onEdit}>
        <ValueNode {...props} />
      </NodeEdit>
    </ReactFlowProvider>,
  );
}

describe("a value drawn compact", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("draws its operator, a chip for a value typed beside its pin, no label, its sentence on hover", () => {
    const less = value("less", [pin("a", "in", "number"), pin("b", "in", "number"), pin("value", "out", "bool")], {
      inputs: { a: "5", b: "80" },
    });
    const { container } = drawn(less, ["a"]);
    expect(container.querySelector(".blueprint-compact")?.getAttribute("title")).toBe("Pods % < 80");
    expect(container.querySelector(".blueprint-compact-op")?.textContent).toBe("<");
    expect([...container.querySelectorAll(".blueprint-chip")].map((chip) => chip.textContent)).toEqual(["80"]);
    expect(container.querySelectorAll(".react-flow__handle")).toHaveLength(3);
    expect(screen.queryByText("A")).toBeNull();
  });

  it("labels its inputs where their parts differ", () => {
    const between = value("between", [
      pin("value", "in", "number"),
      pin("low", "in", "number"),
      pin("high", "in", "number"),
      pin("value", "out", "bool"),
    ]);
    drawn(between, ["value", "low", "high"]);
    expect([screen.getByText("De"), screen.getByText("À")]).toHaveLength(2);
  });

  it("says AND by its word, and its '+' adds an input", () => {
    const onEdit = vi.fn();
    const and = value("and", [pin("in_1", "in", "bool"), pin("in_2", "in", "bool"), pin("value", "out", "bool")], {
      adds: { pin: "in", side: "in", least: 2 },
    });
    const { container } = drawn(and, ["in_1", "in_2"], onEdit);
    expect(container.querySelector(".blueprint-compact-op")?.textContent).toBe("ET");
    fireEvent.click(screen.getByRole("button", { name: "Ajouter une entrée" }));
    expect(onEdit).toHaveBeenCalledWith({ edit: "options", node: "and", count: 3 });
  });
});
