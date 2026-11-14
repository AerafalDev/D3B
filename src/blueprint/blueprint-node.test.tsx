import { cleanup, render, screen } from "@testing-library/react";
import { ReactFlowProvider } from "@xyflow/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { BlueprintNode } from "./blueprint-node.tsx";
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

function node(data: GraphNode) {
  // A node renders its pins through React Flow's handles, which need its provider; the rest of its props play no part.
  const props = { data: { node: data, sentence: [{ text: "Récolter" }] }, selected: false } as unknown as Parameters<
    typeof BlueprintNode
  >[0];
  return render(
    <ReactFlowProvider>
      <BlueprintNode {...props} />
    </ReactFlowProvider>,
  );
}

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

  it("has no tag once the bot does it, and marks a kind this version does not know", () => {
    const { container } = node({ ...harvest, fromStep: null, family: null });
    expect(screen.queryByText(/étape/)).toBeNull();
    expect(container.querySelector(".blueprint-node.is-unknown")).not.toBeNull();
  });
});
