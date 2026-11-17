import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ReactFlowProvider } from "@xyflow/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import type { GraphEdit_Deserialize } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { BlueprintNode } from "./blueprint-node.tsx";
import { NodeEdit } from "./edit/node-options.ts";
import type { GraphNode } from "./node-sentence.ts";

const pin = (id: string, side: "in" | "out", carries: string) =>
  ({ id, side, carries, default: null }) as GraphNode["pins"][number];

const harvest = {
  id: "harvest",
  does: "kind",
  kind: "harvest_until",
  family: "action",
  fromStep: 5,
  pins: [pin("run", "in", "exec"), pin("done", "out", "exec"), pin("failed", "out", "exec"), pin("zone", "in", "zone")],
  inputs: {},
  position: [0, 0],
  event: null,
} as GraphNode;

function node(data: GraphNode, onEdit?: (edit: GraphEdit_Deserialize) => void) {
  // A node renders its pins through React Flow's handles, which need its provider; the rest of its props play no part.
  const props = { data: { node: data, sentence: [{ text: "Récolter" }] }, selected: false } as unknown as Parameters<
    typeof BlueprintNode
  >[0];
  return render(
    <ReactFlowProvider>
      <NodeEdit value={onEdit}>
        <BlueprintNode {...props} />
      </NodeEdit>
    </ReactFlowProvider>,
  );
}

const sequence = {
  ...harvest,
  id: "seq",
  kind: "sequence",
  family: "flow",
  fromStep: null,
  pins: [pin("run", "in", "exec"), pin("then_1", "out", "exec"), pin("then_2", "out", "exec")],
  adds: { pin: "then", side: "out", least: 2 },
} as GraphNode;

const rows = (container: HTMLElement) =>
  [...container.querySelectorAll(".blueprint-row")].map((row) => row.textContent);

describe("a node of the graph", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("fr");
  });
  afterEach(cleanup);

  it("tags an action the bot does not do yet with its step, and labels its pins", () => {
    const { container } = node(harvest);
    expect(screen.getByText("étape 5")).toBeTruthy();
    // Its first flow out is on its head; the others, and its values, are rows with their labels.
    expect([...container.querySelectorAll(".blueprint-row")].map((row) => row.textContent)).toEqual(["Zone", "Échec"]);
    expect(container.querySelectorAll(".blueprint-exec")).toHaveLength(3);
    expect(container.querySelector(".blueprint-value.blueprint-type-zone")).not.toBeNull();
  });

  it("draws a list as a square, ranges hollow, and a wildcard of no type grey until its node notes one", () => {
    const shaped = (more: Partial<GraphNode["pins"][number]>) => ({ ...pin("a", "in", "number"), ...more });
    const ge = {
      ...harvest,
      kind: "greater_or_equal",
      family: "query",
      fromStep: null,
      pins: [
        shaped({ id: "a", wildcard: true }),
        shaped({ id: "z", carries: "zone", list: true }),
        shaped({ id: "r", carries: "zone", ranges: true }),
      ],
    } as GraphNode;
    const { container } = node(ge);
    expect(container.querySelectorAll(".blueprint-open")).toHaveLength(1);
    expect(container.querySelector(".blueprint-open.blueprint-type-number")).not.toBeNull();
    expect(container.querySelector(".blueprint-list.blueprint-type-zone:not(.blueprint-open)")).not.toBeNull();
    expect(container.querySelector(".blueprint-ranges.blueprint-type-zone:not(.blueprint-open)")).not.toBeNull();
    cleanup();
    const typed = node({ ...ge, options: { valueType: "time" }, pins: [shaped({ carries: "time", wildcard: true })] });
    expect(typed.container.querySelector(".blueprint-open")).toBeNull();
    expect(typed.container.querySelector(".blueprint-type-time")).not.toBeNull();
  });

  it("says a Sequence's outputs by their number, each a row, and its '+' adds the next one", () => {
    const onEdit = vi.fn();
    const { container } = node(sequence, onEdit);
    expect(rows(container)).toEqual(["Alors 1", "Alors 2"]);
    fireEvent.click(screen.getByRole("button", { name: "Ajouter une sortie" }));
    expect(onEdit).toHaveBeenCalledWith({ edit: "options", node: "seq", count: 3 });
  });

  it("says AND's inputs by their number, its '+' among them adding the next", () => {
    const onEdit = vi.fn();
    const and = {
      ...sequence,
      id: "and",
      kind: "and",
      family: "value",
      pins: [pin("in_1", "in", "bool"), pin("in_2", "in", "bool"), pin("value", "out", "bool")],
      adds: { pin: "in", side: "in", least: 2 },
    } as GraphNode;
    const { container } = node(and, onEdit);
    expect(rows(container)).toEqual(["Entrée 1", "Entrée 2", "Valeur"]);
    const add = screen.getByRole("button", { name: "Ajouter une entrée" });
    expect(add.closest(".items-end"), "among the inputs, on the left").toBeNull();
    fireEvent.click(add);
    expect(onEdit).toHaveBeenCalledWith({ edit: "options", node: "and", count: 3 });
  });

  it("draws no '+' on a blueprint of D3B's, read-only", () => {
    node(sequence);
    expect(screen.queryByRole("button", { name: "Ajouter une sortie" })).toBeNull();
  });

  it("says a Switch's cases by their values, Otherwise last", () => {
    const switched = {
      ...sequence,
      kind: "switch",
      pins: [
        pin("run", "in", "exec"),
        pin("case_1", "out", "exec"),
        pin("case_2", "out", "exec"),
        pin("otherwise", "out", "exec"),
      ],
      options: { valueType: "number", cases: ["24", "1500"] },
      adds: null,
      takesCases: true,
    } as unknown as GraphNode;
    const { container } = node(switched, vi.fn());
    expect(rows(container)).toEqual(["24", "1 500", "Sinon"]);
    expect(screen.queryByRole("button", { name: "Ajouter une sortie" })).toBeNull();
  });

  it("says a case of a zone by its name, and a case its node no longer holds by its id", async () => {
    const { useZones } = await import("@/zone/zones-store.ts");
    useZones.setState({ zones: [{ id: "ma-foret", name: "Ma forêt", world: 1, maps: [] }] as never });
    const zoned = {
      ...sequence,
      kind: "switch",
      pins: [pin("run", "in", "exec"), pin("case_1", "out", "exec"), pin("case_2", "out", "exec")],
      options: { valueType: "zone", cases: ['{"zone":"ma-foret"}'] },
      adds: null,
      takesCases: true,
    } as unknown as GraphNode;
    const { container } = node(zoned, vi.fn());
    expect(rows(container)).toEqual(["Ma forêt", "case_2"]);
    useZones.setState({ zones: [] });
  });

  it("draws a second flow input, Reset, as a row", () => {
    const once = {
      ...sequence,
      kind: "do_n_times",
      pins: [pin("run", "in", "exec"), pin("reset", "in", "exec"), pin("then", "out", "exec")],
      adds: null,
    } as unknown as GraphNode;
    const { container } = node(once);
    expect(rows(container)).toEqual(["Réarmer"]);
    expect(container.querySelectorAll(".blueprint-exec")).toHaveLength(3);
  });

  it("has no tag once the bot does it, and marks a kind this version does not know", () => {
    const { container } = node({ ...harvest, fromStep: null, family: null });
    expect(screen.queryByText(/étape/)).toBeNull();
    expect(container.querySelector(".blueprint-node.is-unknown")).not.toBeNull();
  });
});
