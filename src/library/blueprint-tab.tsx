import { DndContext, DragOverlay, PointerSensor, pointerWithin, useSensor, useSensors } from "@dnd-kit/core";
import type { IDockviewPanelProps } from "dockview-react";
import { lazy, Suspense, useEffect, useMemo, useRef, useState } from "react";
import { useHotkeys } from "react-hotkeys-hook";
import { useTranslation } from "react-i18next";

import type { GraphEdit_Deserialize, ProblemView } from "@/bindings.ts";
import { BlueprintDetails, type Selection } from "@/blueprint/details/blueprint-details.tsx";
import { draggedVariable, GRAPH_DROP } from "@/blueprint/edit/variable-drop.ts";
import { useGraphs } from "@/blueprint/graph-store.ts";
import { MyBlueprint } from "@/blueprint/my-blueprint.tsx";
import { Summary } from "@/blueprint/summary/summary.tsx";
import { Icon } from "@/icons/icon.tsx";
import { useShare } from "@/share/share-store.ts";
import type { EditorParams } from "@/shell/parts/editor-store.ts";
import { useBlueprintView } from "./blueprint-view.ts";
import { givenText } from "./given-text.ts";
import { InPlace } from "./in-place.tsx";
import { useLibrary } from "./library-store.ts";
import { blueprintName, openBlueprint } from "./open-blueprint.ts";

// React Flow is heavy: the graph is loaded the first time a blueprint is opened.
const BlueprintGraph = lazy(() =>
  import("@/blueprint/blueprint-graph.tsx").then((module) => ({ default: module.BlueprintGraph })),
);

const NO_PROBLEMS: readonly ProblemView[] = [];

interface BlueprintParams extends EditorParams {
  id: string;
}

/**
 * A blueprint's tab, its editor (`docs/bot.md`, "In the app"): on top its name, whose it is, its problems,
 * its description, Duplicate and Export; under them Unreal's "My Blueprint" on the left, the graph it shows, loaded on demand,
 * or its Summary, as the tab strip's toggle says, and Details on the right (the owner, 2026-11-14). One of D3B's is
 * read-only; mine is edited in its graph (step 3's PR 12), Ctrl+Z undoing and Ctrl+Y or Ctrl+Shift+Z redoing its
 * changes, in any of its graphs, while the keyboard is in the tab but not in a field (step 3's PR 13).
 */
export function BlueprintTab({ api, params }: IDockviewPanelProps<BlueprintParams>) {
  const { t, i18n } = useTranslation();
  const blueprint = useLibrary((store) => store.blueprints.find((one) => one.id === params.id));
  const duplicate = useLibrary((store) => store.duplicate);
  const rename = useLibrary((store) => store.rename);
  const exportBlueprint = useShare((store) => store.exportBlueprint);
  const name = blueprint === undefined ? null : blueprintName(blueprint);
  // One copy at a time: a second click while the first is made does nothing.
  const [duplicating, setDuplicating] = useState(false);
  const read_ = useGraphs((store) => store.read[params.id]);
  const load = useGraphs((store) => store.load);
  const edit = useGraphs((store) => store.edit);
  const undo = useGraphs((store) => store.undo);
  const redo = useGraphs((store) => store.redo);
  const refresh = useLibrary((store) => store.refresh);
  const [shown, setShown] = useState<string | null>(null);
  const [selection, setSelection] = useState<Selection>(null);
  const view = useBlueprintView((store) => store.view[api.id] ?? "graph");
  // A variable dragged from My Blueprint onto the graph, once the pointer moved a few pixels: a click still chooses it.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }));
  const [dragging, setDragging] = useState<string | null>(null);
  // The tab's own element, which Ctrl+Z listens on, given the keyboard back when what held it goes.
  const tabBox = useRef<HTMLDivElement | null>(null);

  // Rust undoes and writes (the owner, 2026-11-16); the Library's count of problems follows.
  const editor = useHotkeys<HTMLDivElement>(
    "mod+z, mod+y, mod+shift+z",
    (_event, handler) => {
      const step = handler.hotkey === "mod+z" ? undo : redo;
      void step(params.id).then((saved) => {
        if (saved) {
          void refresh();
        }
      });
    },
    // By the letter typed, not the key's place: Ctrl+Z is on another key in AZERTY and QWERTZ.
    { enabled: blueprint?.given === false, preventDefault: true, useKey: true },
    [params.id, undo, redo, refresh],
  );

  // The variables' and functions' names, as the player reads them, for the nodes that say them; kept between renders,
  // so the graph is drawn again only when they change and keeps the nodes chosen.
  // biome-ignore lint/correctness/useExhaustiveDependencies: the language changes the names, read through i18next
  const names = useMemo(() => {
    if (blueprint === undefined) {
      return new Map<string, string>();
    }
    const read = (path: string, fallback: string) =>
      blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
    const graphs = read_?.state === "read" ? read_.graphs.graphs : [];
    return new Map<string, string>([
      ...blueprint.variables.map(
        (variable) => [variable.id, read(`variables.${variable.id}.name`, variable.name)] as const,
      ),
      ...graphs
        .filter((graph) => graph.function)
        .map((graph) => [graph.id, read(`graphs.${graph.id}`, graph.name)] as const),
    ]);
  }, [blueprint, read_, i18n.language]);

  useEffect(() => {
    void load(params.id);
  }, [load, params.id]);

  useEffect(() => {
    if (name !== null) {
      api.setTitle(name);
    }
  }, [api, name]);

  if (blueprint === undefined || name === null) {
    return <p className="text-text-muted flex h-full items-center justify-center p-6 text-xs">{t("library.gone")}</p>;
  }
  const read = (path: string, fallback: string) =>
    blueprint.given ? givenText(blueprint.id, path, fallback) : fallback;
  const graphs = read_?.state === "read" ? read_.graphs.graphs : [];
  const shownGraph = graphs.find((graph) => graph.id === shown) ?? graphs[0];
  // A variable's edit is the blueprint's, whichever graph shows; the Library's variables and problems follow.
  const blueprintEdit = (change: GraphEdit_Deserialize) =>
    edit(blueprint.id, shownGraph?.id ?? "", change).then(async (saved) => {
      if (saved) {
        await refresh();
      }
      return saved;
    });
  const addVariable = () => {
    const before = new Set(blueprint.variables.map((variable) => variable.id));
    void blueprintEdit({ edit: "newVariable", name: t("blueprint.newVariable") }).then(() => {
      // The new one is the variable the Library did not list before: Details shows it, to be named and typed.
      const now = useLibrary.getState().blueprints.find((one) => one.id === blueprint.id);
      const made = now?.variables.find((variable) => !before.has(variable.id));
      if (made !== undefined) {
        setSelection({ kind: "variable", id: made.id });
      }
    });
  };
  // The nodes of the graph shown that a problem names, marked in the graph.
  const problems = read_?.state === "read" ? read_.graphs.problems : NO_PROBLEMS;
  const faulty = problems
    .filter((problem) => problem.graph === shownGraph?.id)
    .flatMap((problem) => (problem.node == null ? [] : [problem.node]));
  const whose = blueprint.given
    ? t("library.givenLine", { version: blueprint.version })
    : blueprint.basedOn === null
      ? t("library.mineLine")
      : [
          t("library.mineBasedOn", {
            name: givenText(blueprint.basedOn.id, "name", blueprint.basedOn.id),
            version: blueprint.basedOn.version,
          }),
          blueprint.basedOn.newer == null ? null : t("library.originalNewerLine", { version: blueprint.basedOn.newer }),
        ]
          .filter(Boolean)
          .join(" · ");

  return (
    <div
      ref={(element) => {
        tabBox.current = element;
        editor(element);
      }}
      tabIndex={-1}
      className="flex h-full min-h-0 flex-col outline-none"
    >
      <div className="border-line flex flex-col gap-1 border-b px-5 py-3">
        <header className="flex items-center gap-2.5">
          <Icon name="blueprint" className="size-[22px]" />
          <div className="min-w-0 flex-1">
            {blueprint.given ? (
              <h2 className="truncate text-[15px] font-medium text-white">{name}</h2>
            ) : (
              <h2 className="flex text-[15px] font-medium text-white">
                <InPlace
                  value={blueprint.name}
                  label={t("library.name")}
                  blank={false}
                  onSave={(named) => rename(blueprint, named, blueprint.description)}
                />
              </h2>
            )}
            <p className="text-text-muted text-xs">
              {whose}
              {blueprint.problems > 0 && (
                <span className="text-[#d29922]"> · {t("library.problems", { count: blueprint.problems })}</span>
              )}
            </p>
          </div>
          <button
            type="button"
            disabled={duplicating}
            onClick={() => {
              setDuplicating(true);
              void duplicate(blueprint).then((id) => {
                setDuplicating(false);
                if (id !== null) {
                  openBlueprint(id, t("library.copyName", { name }));
                }
              });
            }}
            className="bg-focus hover:bg-focus/90 focus-visible:outline-focus flex items-center gap-1.5 rounded px-3 py-1 text-xs text-white focus-visible:outline-1 focus-visible:outline-offset-2"
          >
            <Icon name="copy" className="size-3.5 text-white" />
            {t("library.duplicate")}
          </button>
          <button
            type="button"
            onClick={() => void exportBlueprint(blueprint)}
            className="hover:bg-hover focus-visible:outline-focus flex items-center gap-1.5 rounded border border-[#3c3c3c] px-3 py-1 text-xs focus-visible:outline-1 focus-visible:outline-offset-2"
          >
            <Icon name="fileExport" className="size-3.5" />
            {t("share.export")}
          </button>
        </header>
        {blueprint.given ? (
          <p className="text-text-muted truncate text-xs" title={read("description", blueprint.description)}>
            {read("description", blueprint.description)}
          </p>
        ) : (
          <p className="text-text-muted flex text-xs">
            <InPlace
              value={blueprint.description}
              label={t("library.descriptionLabel")}
              placeholder={t("library.describe")}
              blank
              onSave={(described) => rename(blueprint, blueprint.name, described)}
            />
          </p>
        )}
      </div>
      <DndContext
        sensors={sensors}
        // The graph under the pointer, not under the name it drags; nothing scrolls under a drag, whose delta would
        // count it.
        collisionDetection={pointerWithin}
        autoScroll={false}
        accessibility={{
          announcements: {
            onDragStart: ({ active }) =>
              t("blueprint.drag.start", { name: names.get(draggedVariable(active.data.current) ?? "") ?? "" }),
            onDragOver: () => undefined,
            onDragEnd: ({ over }) => t(over?.id === GRAPH_DROP ? "blueprint.drag.end" : "blueprint.drag.cancel"),
            onDragCancel: () => t("blueprint.drag.cancel"),
          },
          screenReaderInstructions: { draggable: t("blueprint.drag.instructions") },
        }}
        onDragStart={({ active }) => setDragging(draggedVariable(active.data.current))}
        onDragEnd={() => setDragging(null)}
        onDragCancel={() => setDragging(null)}
      >
        <div className="grid min-h-0 flex-1 grid-cols-[220px_1fr_280px]">
          {read_ === undefined || read_.state === "loading" ? (
            <p className="text-text-muted col-span-3 p-6 text-xs">{t("blueprint.loading")}</p>
          ) : read_.state === "failed" ? (
            <p className="text-text-muted col-span-3 p-6 text-xs">
              {read_.error.kind === "noSuchBlueprint"
                ? t("library.gone")
                : t("blueprint.bug", { reason: read_.error.reason })}
            </p>
          ) : (
            <>
              <MyBlueprint
                blueprint={blueprint}
                graphs={read_.graphs.graphs}
                shown={shownGraph?.id ?? ""}
                onShow={(graph) => {
                  setShown(graph);
                  setSelection(null);
                }}
                chosen={selection?.kind === "variable" ? selection.id : null}
                onChoose={(variable) => setSelection({ kind: "variable", id: variable })}
                onAddVariable={blueprint.given ? undefined : addVariable}
              />
              {view === "summary" ? (
                <Summary
                  graphs={graphs}
                  names={names}
                  chosen={
                    selection?.kind === "node" && shownGraph !== undefined
                      ? { graph: shownGraph.id, node: selection.id }
                      : null
                  }
                  onChoose={(graph, node) => {
                    setShown(graph);
                    setSelection({ kind: "node", id: node });
                  }}
                />
              ) : shownGraph === undefined ? (
                <div />
              ) : (
                <Suspense fallback={<p className="text-text-muted p-6 text-xs">{t("blueprint.loading")}</p>}>
                  <BlueprintGraph
                    // One graph per mount: a picker open on one never adds to another.
                    key={shownGraph.id}
                    graph={shownGraph}
                    names={names}
                    faulty={faulty}
                    chosen={selection?.kind === "node" ? selection.id : null}
                    onChoose={(node) => setSelection(node === null ? null : { kind: "node", id: node })}
                    // D3B's blueprints are locked; mine are edited in place, the Library's count of problems after.
                    onEdit={
                      blueprint.given
                        ? undefined
                        : (change) =>
                            void edit(blueprint.id, shownGraph.id, change).then((saved) => {
                              if (saved) {
                                void refresh();
                              }
                            })
                    }
                  />
                </Suspense>
              )}
              <BlueprintDetails
                blueprint={blueprint}
                graphs={graphs}
                graph={shownGraph}
                problems={read_.graphs.problems}
                selection={selection}
                names={names}
                onVariable={blueprint.given ? undefined : (variable) => blueprintEdit({ edit: "variable", variable })}
                onDeleteVariable={
                  blueprint.given
                    ? undefined
                    : (variable) => {
                        setSelection(null);
                        // The button goes with it: the tab keeps the keyboard, so Ctrl+Z brings it back at once.
                        tabBox.current?.focus({ preventScroll: true });
                        void blueprintEdit({ edit: "deleteVariable", variable });
                      }
                }
                onValue={
                  blueprint.given || shownGraph === undefined
                    ? undefined
                    : (node, pin, value) =>
                        edit(blueprint.id, shownGraph.id, { edit: "value", node, pin, value }).then((saved) => {
                          if (saved) {
                            void refresh();
                          }
                          return saved;
                        })
                }
              />
            </>
          )}
        </div>
        <DragOverlay dropAnimation={null}>
          {dragging === null ? null : (
            <span className="flex w-max items-center gap-2 rounded-[5px] border border-[#3c3c3c] bg-[#2a2c30] px-2 py-1 text-[13px] text-white shadow-lg">
              {names.get(dragging) ?? dragging}
            </span>
          )}
        </DragOverlay>
      </DndContext>
    </div>
  );
}
