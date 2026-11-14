import "@xyflow/react/dist/style.css";
import "./blueprint-graph.css";

import {
  Background,
  BackgroundVariant,
  Controls,
  type Edge,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
} from "@xyflow/react";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import type { GraphView_Serialize } from "@/bindings.ts";
import { BlueprintNode, type BoxNode, VariablePill } from "./blueprint-node.tsx";
import { graphSentences } from "./node-sentence.ts";

const TYPES = { box: BlueprintNode, pill: VariablePill };

interface BlueprintGraphProps {
  graph: GraphView_Serialize;
  /** The blueprint's variables' and functions' names by id, as the player reads them. */
  names: ReadonlyMap<string, string>;
}

// Where a node the file does not place goes: a column on the left, one under the other.
const UNPLACED_X = -320;
const UNPLACED_STEP = 120;

/**
 * A blueprint's graph, read-only (`docs/bot.md`, "In the app"): its nodes where the file puts them, its wires coloured
 * by what they carry, pan and zoom, a mini-map. Editing comes with step 3's PR 13. Loaded on demand, React Flow being
 * heavy.
 */
export default function BlueprintGraph(props: BlueprintGraphProps) {
  return (
    <ReactFlowProvider>
      <Graph {...props} />
    </ReactFlowProvider>
  );
}

function Graph({ graph, names }: BlueprintGraphProps) {
  const { i18n } = useTranslation();
  // biome-ignore lint/correctness/useExhaustiveDependencies: the language changes every sentence, read through i18next
  const { nodes, edges } = useMemo(() => {
    let unplaced = 0;
    const byId = new Map(graph.nodes.map((node) => [node.id, node]));
    const sentences = graphSentences(graph, names);
    const nodes = graph.nodes.map((node) => {
      const [x, y] = node.position ?? [UNPLACED_X, UNPLACED_STEP * unplaced++];
      const position = { x, y };
      if (node.does === "get") {
        const valueType = node.pins[0]?.carries ?? "text";
        return {
          id: node.id,
          type: "pill" as const,
          position,
          data: { name: names.get(node.variable) ?? node.variable, valueType },
        };
      }
      return {
        id: node.id,
        type: "box" as const,
        position,
        data: { node, sentence: sentences.get(node.id) ?? [] },
      } satisfies BoxNode;
    });
    const edges: Edge[] = graph.wires.map(({ from, to }) => {
      const carries =
        byId.get(from[0])?.pins.find((pin) => pin.id === from[1] && pin.side === "out")?.carries ?? "exec";
      return {
        id: `${from.join(".")}>${to.join(".")}`,
        source: from[0],
        sourceHandle: from[1],
        target: to[0],
        targetHandle: to[1],
        className: `blueprint-type-${carries}`,
      };
    });
    return { nodes, edges };
  }, [graph, names, i18n.language]);

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
