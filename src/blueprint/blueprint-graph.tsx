import "@xyflow/react/dist/style.css";
import "./blueprint-graph.css";

import { Background, BackgroundVariant, Controls, MiniMap, ReactFlow, ReactFlowProvider } from "@xyflow/react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import type { GraphView_Serialize } from "@/bindings.ts";
import { BlueprintNode } from "./blueprint-node.tsx";
import { graphElements } from "./graph-elements.ts";
import { VariablePill } from "./variable-pill.tsx";

const TYPES = { box: BlueprintNode, pill: VariablePill };

interface BlueprintGraphProps {
  graph: GraphView_Serialize;
  /** The blueprint's variables' and functions' names by id, as the player reads them. */
  names: ReadonlyMap<string, string>;
  /** The nodes that have a problem, marked. */
  faulty?: readonly string[];
  /** A node chosen, or `null` when the pane is. */
  onChoose?: (node: string | null) => void;
}

/**
 * A blueprint's graph, read-only (`docs/bot.md`, "In the app"): its nodes where the file puts them, its wires coloured
 * by what they carry, pan and zoom, a mini-map. Editing comes with step 3's PR 13. Loaded on demand by the blueprint's
 * tab, React Flow being heavy.
 */
export function BlueprintGraph(props: BlueprintGraphProps) {
  return (
    <ReactFlowProvider>
      <Graph {...props} />
    </ReactFlowProvider>
  );
}

function Graph({ graph, names, faulty, onChoose }: BlueprintGraphProps) {
  const { t, i18n } = useTranslation();
  // The faulty nodes by their ids, so the same problems keep the same drawing.
  const faultyKey = (faulty ?? []).join("|");
  // biome-ignore lint/correctness/useExhaustiveDependencies: the language changes every sentence, read through i18next
  const { nodes, edges } = useMemo(
    () => graphElements(graph, names, new Set(faultyKey === "" ? [] : faultyKey.split("|"))),
    [graph, names, faultyKey, i18n.language],
  );
  // React Flow's own words, in the player's language.
  const ariaLabelConfig = {
    "controls.ariaLabel": t("blueprint.controls"),
    "controls.zoomIn.ariaLabel": t("blueprint.zoomIn"),
    "controls.zoomOut.ariaLabel": t("blueprint.zoomOut"),
    "controls.fitView.ariaLabel": t("blueprint.fitView"),
    "minimap.ariaLabel": t("blueprint.minimap"),
  };

  return (
    <ReactFlow
      className="blueprint-flow"
      colorMode="dark"
      nodes={nodes}
      edges={edges}
      nodeTypes={TYPES}
      nodesDraggable={false}
      nodesConnectable={false}
      edgesFocusable={false}
      ariaLabelConfig={ariaLabelConfig}
      onNodeClick={(_, node) => onChoose?.(node.id)}
      onPaneClick={() => onChoose?.(null)}
      fitView
      fitViewOptions={{ padding: 0.15 }}
      minZoom={0.3}
      proOptions={{ hideAttribution: true }}
    >
      <Background variant={BackgroundVariant.Dots} gap={16} size={1} />
      <MiniMap pannable zoomable nodeColor="#3a3a3a" maskColor="rgb(17 17 17 / 60%)" />
      <Controls showInteractive={false} />
    </ReactFlow>
  );
}
