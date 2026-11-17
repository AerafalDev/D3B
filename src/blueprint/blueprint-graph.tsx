import "@xyflow/react/dist/style.css";
import "./blueprint-graph.css";

import { useDndMonitor, useDroppable } from "@dnd-kit/core";

import {
  applyEdgeChanges,
  applyNodeChanges,
  Background,
  BackgroundVariant,
  Controls,
  type Edge,
  ReactFlow,
  ReactFlowProvider,
  useReactFlow,
  type XYPosition,
} from "@xyflow/react";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import type { ChoiceView_Serialize, GraphEdit_Deserialize, GraphView_Serialize, NodeDoes } from "@/bindings.ts";
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuShortcut,
  ContextMenuTrigger,
} from "@/components/ui/context-menu.tsx";
import type { BoxNode } from "./blueprint-node.tsx";
import { BlueprintNode } from "./blueprint-node.tsx";
import { ConnectionLine } from "./edit/connection-line.tsx";
import { deletion, type MenuTarget, menuDeletion } from "./edit/deletion.ts";
import { GraphPins, type PinOf } from "./edit/graph-pins.ts";
import { type LinkEnd, linkRefusal } from "./edit/link-check.ts";
import { Picker } from "./edit/picker.tsx";
import { keptSelection } from "./edit/selection.ts";
import { GRAPH_DROP, type Pointer, variableDropped } from "./edit/variable-drop.ts";
import { VariableMenu } from "./edit/variable-menu.tsx";
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
 * why; dropped on the ground, Space or a right-click opens the picker; a node moved keeps its place. Delete and
 * Backspace with the keyboard in the graph, or a node's or a wire's right-click, delete what is chosen, a node's wires
 * with it, without asking (step 3's PR 13, the owner's choice of 2026-11-16: undo brings it back). Loaded on demand by
 * the blueprint's tab, React Flow being heavy.
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
  // A variable dropped from My Blueprint, asking whether to read or write it: where, and which.
  const [asking, setAsking] = useState<{ client: { x: number; y: number }; at: XYPosition; variable: string } | null>(
    null,
  );
  const editable = onEdit !== undefined;
  // The pointer as it last moved, and the keys held, read when a variable is dropped: `@dnd-kit` gives the drop no
  // event, and its delta counts the scroll of what the row was dragged from.
  const last = useRef<Pointer | null>(null);
  useEffect(() => {
    const read = (event: KeyboardEvent | PointerEvent) => {
      const at = "clientX" in event ? { x: event.clientX, y: event.clientY } : (last.current ?? { x: 0, y: 0 });
      last.current = { ...at, ctrlKey: event.ctrlKey, metaKey: event.metaKey, altKey: event.altKey };
    };
    for (const type of ["pointermove", "keydown", "keyup"] as const) {
      window.addEventListener(type, read);
    }
    return () => {
      for (const type of ["pointermove", "keydown", "keyup"] as const) {
        window.removeEventListener(type, read);
      }
    };
  }, []);
  const { setNodeRef: dropOn } = useDroppable({ id: GRAPH_DROP, disabled: onEdit === undefined });
  // A variable dragged from My Blueprint is read with Ctrl, written with Alt, else a small menu asks.
  useDndMonitor({
    onDragEnd: ({ active, over }) => {
      const dropped = variableDropped(active.data.current, over?.id === GRAPH_DROP, editable, last.current, (client) =>
        flow.screenToFlowPosition(client),
      );
      if (dropped === null) {
        return;
      }
      // One overlay at a time: the picker open gives way.
      setOpened(null);
      if (dropped.kind === "ask") {
        setAsking(dropped);
      } else {
        onEdit?.(dropped.edit);
      }
    },
  });
  const addVariable = (does: "get" | "set", variable: string, at: XYPosition) =>
    onEdit?.({
      edit: "add",
      node: { does, variable },
      position: [Math.round(at.x), Math.round(at.y)],
      wiredTo: null,
    });
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
  // The nodes as dragged and the nodes and wires as chosen, until the graph saved comes back, which keeps the choice.
  const [nodes, setNodes] = useState<(BoxNode | PillNode)[]>(drawn.nodes);
  const [edges, setEdges] = useState<Edge[]>(drawn.edges);
  const shown = useRef({ nodes, edges });
  shown.current = { nodes, edges };
  useEffect(() => {
    const kept = keptSelection(drawn, shown.current, chosen);
    setNodes(kept.nodes);
    setEdges(kept.edges);
  }, [drawn, chosen]);
  // What the right-click under way landed on: a node or a wire opens the menu, the pane the picker, the rest nothing.
  const target = useRef<MenuTarget | null>(null);
  // What the menu open deletes.
  const menuFor = useRef<MenuTarget | null>(null);
  const remove = (change: GraphEdit_Deserialize | null) => {
    if (change !== null) {
      onEdit?.(change);
      // The node focused goes with it: the graph keeps the keyboard, so Ctrl+Z undoes at once.
      box.current?.focus({ preventScroll: true });
    }
  };
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
      <ContextMenu modal={false}>
        <ContextMenuTrigger
          asChild
          disabled={!editable}
          onContextMenu={(event) => {
            // Nothing deletable under the pointer (the pane opened the picker): no menu.
            menuFor.current = target.current;
            target.current = null;
            if (menuFor.current === null) {
              event.preventDefault();
            }
          }}
        >
          {/* biome-ignore lint/a11y/noStaticElementInteractions: Space opens the picker at the pointer over the graph */}
          <div
            ref={(element) => {
              box.current = element;
              dropOn(element);
            }}
            tabIndex={-1}
            className="relative h-full w-full outline-none"
            onKeyDown={(event) => {
              if (!editable || opened !== null) {
                return;
              }
              // The graph itself focused (a click on it), not a node, whose Space chooses it.
              if (event.key === " " && event.target === event.currentTarget) {
                event.preventDefault();
                open(pointer.current, null);
              }
              // Delete and Backspace with the keyboard in this graph only, never in another tab's or out of sight;
              // Rust deletes, one change undone at once, and the graph saved comes back without them.
              const typing =
                event.target instanceof Element && event.target.closest("input, textarea, [contenteditable]");
              if ((event.key === "Delete" || event.key === "Backspace") && !typing) {
                event.preventDefault();
                const chosenNodes = nodes.filter((node) => node.selected === true).map((node) => node.id);
                remove(
                  deletion(
                    chosenNodes,
                    edges.filter((edge) => edge.selected === true),
                  ),
                );
              }
            }}
          >
            <ReactFlow
              className="blueprint-flow"
              colorMode="dark"
              nodes={nodes}
              edges={edges}
              nodeTypes={TYPES}
              edgeTypes={WIRES}
              nodesDraggable={editable}
              nodesConnectable={editable}
              // React Flow listens to the whole window: the graph's own keys delete instead (above).
              deleteKeyCode={null}
              edgesFocusable={editable}
              onEdgesChange={(changes) => setEdges((now) => applyEdgeChanges(changes, now))}
              onNodeContextMenu={(_, node) => {
                target.current = editable ? { kind: "node", id: node.id } : null;
              }}
              onEdgeContextMenu={(_, edge) => {
                target.current = editable ? { kind: "wire", id: edge.id } : null;
              }}
              // Nodes chosen by a box lie under its rectangle, which takes the right-click: the whole selection.
              onSelectionContextMenu={(_, chosenNodes) => {
                const first = chosenNodes[0];
                target.current = editable && first !== undefined ? { kind: "node", id: first.id } : null;
              }}
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
                  open(
                    { x: at.clientX, y: at.clientY },
                    pinOf(state.fromHandle.nodeId, state.fromHandle.id ?? null, side),
                  );
                }
              }}
              onPaneContextMenu={(event) => {
                target.current = null;
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
                    wiredTo:
                      dragged === null ? null : { node: dragged.node, pin: dragged.pin.id, side: dragged.pin.side },
                  });
                  close();
                }}
              />
            )}
            {asking !== null && (
              <VariableMenu
                at={{ x: asking.client.x - (corner?.left ?? 0), y: asking.client.y - (corner?.top ?? 0) }}
                name={names.get(asking.variable) ?? asking.variable}
                onPick={(does) => {
                  addVariable(does, asking.variable, asking.at);
                  setAsking(null);
                  box.current?.focus({ preventScroll: true });
                }}
                onClose={() => {
                  // The graph keeps the keyboard, for Space, Delete and Ctrl+Z, as when the picker closes.
                  setAsking(null);
                  box.current?.focus({ preventScroll: true });
                }}
              />
            )}
          </div>
        </ContextMenuTrigger>
        <ContextMenuContent
          onCloseAutoFocus={(event) => {
            // The graph keeps the keyboard, for Delete and Space, unless what opened since (the picker) took it.
            event.preventDefault();
            if (!box.current?.contains(document.activeElement)) {
              box.current?.focus({ preventScroll: true });
            }
          }}
        >
          <ContextMenuItem
            onSelect={() => {
              const chosen = menuFor.current;
              remove(chosen === null ? null : menuDeletion(chosen, nodes, edges));
            }}
          >
            {t("blueprint.delete")}
            <ContextMenuShortcut>{t("blueprint.deleteKey")}</ContextMenuShortcut>
          </ContextMenuItem>
        </ContextMenuContent>
      </ContextMenu>
    </GraphPins>
  );
}
