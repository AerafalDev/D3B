import type { IDockviewPanelProps } from "dockview-react";
import { lazy, Suspense, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import type { ProblemView } from "@/bindings.ts";
import { BlueprintDetails, type Selection } from "@/blueprint/details/blueprint-details.tsx";
import { useGraphs } from "@/blueprint/graph-store.ts";
import { MyBlueprint } from "@/blueprint/my-blueprint.tsx";
import { Icon } from "@/icons/icon.tsx";
import type { EditorParams } from "@/shell/parts/editor-store.ts";
import { givenText } from "./given-text.ts";
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
 * A blueprint's tab, its editor read-only (`docs/bot.md`, "In the app"): on top its name, whose it is, its problems,
 * its description and Duplicate; under them Unreal's "My Blueprint" on the left, the graph it shows, loaded on demand,
 * and Details on the right (the owner, 2026-11-14). The Summary comes with step 3's PR 8c, editing with PR 13.
 */
export function BlueprintTab({ api, params }: IDockviewPanelProps<BlueprintParams>) {
  const { t } = useTranslation();
  const blueprint = useLibrary((store) => store.blueprints.find((one) => one.id === params.id));
  const duplicate = useLibrary((store) => store.duplicate);
  const name = blueprint === undefined ? null : blueprintName(blueprint);
  // One copy at a time: a second click while the first is made does nothing.
  const [duplicating, setDuplicating] = useState(false);
  const read_ = useGraphs((store) => store.read[params.id]);
  const load = useGraphs((store) => store.load);
  const [shown, setShown] = useState<string | null>(null);
  const [selection, setSelection] = useState<Selection>(null);

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
  // The variables' and functions' names, as the player reads them, for the nodes that say them.
  const names = new Map<string, string>([
    ...blueprint.variables.map(
      (variable) => [variable.id, read(`variables.${variable.id}.name`, variable.name)] as const,
    ),
    ...graphs
      .filter((graph) => graph.function)
      .map((graph) => [graph.id, read(`graphs.${graph.id}`, graph.name)] as const),
  ]);
  // The nodes of the graph shown that a problem names, marked in the graph.
  const problems = read_?.state === "read" ? read_.graphs.problems : NO_PROBLEMS;
  const faulty = problems
    .filter((problem) => problem.graph === shownGraph?.id)
    .flatMap((problem) => (problem.node == null ? [] : [problem.node]));
  const whose = blueprint.given
    ? t("library.givenLine", { version: blueprint.version })
    : blueprint.basedOn === null
      ? t("library.mineLine")
      : t("library.mineBasedOn", {
          name: givenText(blueprint.basedOn.id, "name", blueprint.basedOn.id),
          version: blueprint.basedOn.version,
        });

  return (
    <div className="flex h-full min-h-0 flex-col">
      <div className="border-line flex flex-col gap-1 border-b px-5 py-3">
        <header className="flex items-center gap-2.5">
          <Icon name="blueprint" className="size-[22px]" />
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-[15px] font-medium text-white">{name}</h2>
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
        </header>
        <p className="text-text-muted truncate text-xs" title={read("description", blueprint.description)}>
          {read("description", blueprint.description)}
        </p>
      </div>
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
            />
            {shownGraph === undefined ? (
              <div />
            ) : (
              <Suspense fallback={<p className="text-text-muted p-6 text-xs">{t("blueprint.loading")}</p>}>
                <BlueprintGraph
                  graph={shownGraph}
                  names={names}
                  faulty={faulty}
                  chosen={selection?.kind === "node" ? selection.id : null}
                  onChoose={(node) => setSelection(node === null ? null : { kind: "node", id: node })}
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
            />
          </>
        )}
      </div>
    </div>
  );
}
