import "@xyflow/react/dist/style.css";
import "./blueprint-graph.css";

import {
  applyNodeChanges,
  Background,
  BackgroundVariant,
  Controls,
  ReactFlow,
  ReactFlowProvider,
  useReactFlow,
} from "@xyflow/react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import type { ChoiceView_Serialize, GraphEdit_Deserialize, GraphView_Serialize, NodeDoes } from "@/bindings.ts";
import type { BoxNode } from "./blueprint-node.tsx";
import { BlueprintNode } from "./blueprint-node.tsx";
import { ConnectionLine } from "./edit/connection-line.tsx";
import { GraphPins, type PinOf } from "./edit/graph-pins.ts";
import { type LinkEnd, linkRefusal } from "./edit/link-check.ts";
import { Picker } from "./edit/picker.tsx";
import { chosenNode, graphElements } from "./graph-elements.ts";
import { type PillNode, VariablePill } from "./variable-pill.tsx";
import { WireEdge } from "./wire-edge.tsx";

const TYPES = { box: BlueprintNode, pill: VariablePill };
const WIRES = { wire: WireEdge };

interface BlueprintGraphProps {
  graph: GraphView_Serialize;
  /** The blueprint's variables' and functions' names by id, as the player reads them. */
  names: ReadonlyMap<string, string>;
  /** The nodes that have a problem, marked. */
  faulty?: readonly string[];
  /** The node Details shows, drawn chosen. */
  chosen?: string | null;
  /** A node chosen, or `null` when the pane is. */
  onChoose?: (node: string | null) => void;
  /** A change to the graph, for a blueprint of mine; none for one of D3B's, read-only. */
  onEdit?: (edit: GraphEdit_Deserialize) => void;
}

/** What a choice does, as an edit names it. */
function doesOf(choice: ChoiceView_Serialize): NodeDoes {
  switch (choice.does) {
    case "kind":
      return { does: "kind", kind: choice.kind };
    case "get":
      return { does: "get", variable: choice.variable };
    case "set":
      return { does: "set", variable: choice.variable };
    case "call":
      return { does: "call", function: choice.function };
  }
}

/**
 * Where the picker is open: the pointer on the screen, the graph's point under it then, where the node goes, and the
 * pin a wire dropped on the ground came from.
 */
type Opened = { client: { x: number; y: number }; at: { x: number; y: number }; dragged: LinkEnd | null };

/**
 * A blueprint's graph (`docs/bot.md`, "In the app"): its nodes where the file puts them, its wires coloured by what
 * they carry, pan and zoom, no mini-map (of no use: the owner, 2026-11-16). A blueprint of mine is edited in place
 * (step 3's PR 12, the owner's choices of 2026-11-16): a wire dragged between pins is checked by type, refused with
 * why; dropped on the ground, Space or a right-click opens the picker; a node moved keeps its place. Loaded on demand
 * by the blueprint's tab, React Flow being heavy.
 */
export function BlueprintGraph(props: BlueprintGraphProps) {
  return (
    <ReactFlowProvider>
      <Graph {...props} />
    </ReactFlowProvider>
  );
}

function Graph({ graph, names, faulty, chosen = null, onChoose, onEdit }: BlueprintGraphProps) {
  const { t, i18n } = useTranslation();
  const flow = useReactFlow();
  const box = useRef<HTMLDivElement>(null);
  // The pointer's last place on the pane, where Space opens the picker.
  const pointer = useRef({ x: 0, y: 0 });
  const [opened, setOpened] = useState<Opened | null>(null);
  const editable = onEdit !== undefined;
  const open = (client: { x: number; y: number }, dragged: LinkEnd | null) =>
    setOpened({ client, at: flow.screenToFlowPosition(client), dragged });
  // The picker closed, the graph keeps the keyboard, so Space opens it again.
  const close = () => {
    setOpened(null);
    box.current?.focus({ preventScroll: true });
  };
  // The faulty nodes by their ids, so the same problems keep the same drawing.
  const faultyKey = (faulty ?? []).join("|");
  // biome-ignore lint/correctness/useExhaustiveDependencies: the language changes every sentence, read through i18next
  const drawn = useMemo(
    () => graphElements(graph, names, new Set(faultyKey === "" ? [] : faultyKey.split("|")), chosen),
    [graph, names, faultyKey, chosen, i18n.language],
  );
  // The nodes as dragged, until the graph saved comes back.
  const [nodes, setNodes] = useState<(BoxNode | PillNode)[]>(drawn.nodes);
  useEffect(() => setNodes(drawn.nodes), [drawn.nodes]);
  const pinOf = useMemo<PinOf>(() => {
    const byNode = new Map(graph.nodes.map((node) => [node.id, node.pins]));
    return (node, pin, side) => {
      const found = byNode.get(node)?.find((one) => one.id === pin && one.side === side);
      return found === undefined ? null : { node, pin: found };
    };
  }, [graph]);
  // React Flow's own words, in the player's language.
  const ariaLabelConfig = {
    "controls.ariaLabel": t("blueprint.controls"),
    "controls.zoomIn.ariaLabel": t("blueprint.zoomIn"),
    "controls.zoomOut.ariaLabel": t("blueprint.zoomOut"),
    "controls.fitView.ariaLabel": t("blueprint.fitView"),
  };
  const corner = box.current?.getBoundingClientRect();

  return (
    <GraphPins value={pinOf}>
      {/* biome-ignore lint/a11y/noStaticElementInteractions: Space opens the picker at the pointer over the graph */}
      <div
        ref={box}
        tabIndex={-1}
        className="relative h-full w-full outline-none"
        onKeyDown={(event) => {
          // The graph itself focused (a click on it), not a node, whose Space chooses it.
          if (editable && opened === null && event.key === " " && event.target === event.currentTarget) {
            event.preventDefault();
            open(pointer.current, null);
          }
        }}
      >
        <ReactFlow
          className="blueprint-flow"
          colorMode="dark"
          nodes={nodes}
          edges={drawn.edges}
          nodeTypes={TYPES}
          edgeTypes={WIRES}
          nodesDraggable={editable}
          nodesConnectable={editable}
          // Deleting comes with step 3's PR 13: until then a key never takes a node off the view.
          deleteKeyCode={null}
          edgesFocusable={false}
          ariaLabelConfig={ariaLabelConfig}
          connectionLineComponent={ConnectionLine}
          isValidConnection={(wire) => {
            const from = pinOf(wire.source, wire.sourceHandle ?? null, "out");
            const to = pinOf(wire.target, wire.targetHandle ?? null, "in");
            return from !== null && to !== null && linkRefusal(from, to) === null;
          }}
          onConnect={(wire) =>
            onEdit?.({
              edit: "wire",
              from: [wire.source, wire.sourceHandle ?? ""],
              to: [wire.target, wire.targetHandle ?? ""],
            })
          }
          onConnectEnd={(event, state) => {
            // Dropped on the ground only: over a pin, the wire was made or refused.
            if (!editable || state.isValid || state.toHandle !== null || state.fromHandle === null) {
              return;
            }
            const at = "changedTouches" in event ? event.changedTouches[0] : event;
            const side = state.fromHandle.type === "source" ? "out" : "in";
            if (at !== undefined) {
              open({ x: at.clientX, y: at.clientY }, pinOf(state.fromHandle.nodeId, state.fromHandle.id ?? null, side));
            }
          }}
          onPaneContextMenu={(event) => {
            if (editable) {
              event.preventDefault();
              open({ x: event.clientX, y: event.clientY }, null);
            }
          }}
          onPaneMouseMove={(event) => {
            pointer.current = { x: event.clientX, y: event.clientY };
          }}
          // A click or Enter on a focused node: the nodes are the tab's, so their selection is read from the changes.
          onNodesChange={(changes) => {
            setNodes((now) => applyNodeChanges(changes, now));
            const node = chosenNode(changes);
            if (node !== null) {
              onChoose?.(node);
            }
          }}
          // Every node dragged together keeps its place, in one change, undone at once.
          onNodeDragStop={(_, _node, dragged) => {
            const nodes = dragged.map((node) => ({
              node: node.id,
              position: [Math.round(node.position.x), Math.round(node.position.y)] as [number, number],
            }));
            onEdit?.({ edit: "move", nodes });
          }}
          onPaneClick={() => {
            close();
            onChoose?.(null);
          }}
          fitView
          fitViewOptions={{ padding: 0.15 }}
          minZoom={0.3}
          proOptions={{ hideAttribution: true }}
        >
          <Background variant={BackgroundVariant.Dots} gap={16} size={1} />
          <Controls showInteractive={false} />
        </ReactFlow>
        {opened !== null && (
          <Picker
            at={{ x: opened.client.x - (corner?.left ?? 0), y: opened.client.y - (corner?.top ?? 0) }}
            choices={graph.choices}
            dragged={opened.dragged}
            names={names}
            onClose={close}
            onPick={(choice) => {
              const { at, dragged } = opened;
              onEdit?.({
                edit: "add",
                node: doesOf(choice),
                position: [Math.round(at.x), Math.round(at.y)],
                wiredTo: dragged === null ? null : { node: dragged.node, pin: dragged.pin.id, side: dragged.pin.side },
              });
              close();
            }}
          />
        )}
      </div>
    </GraphPins>
  );
}
