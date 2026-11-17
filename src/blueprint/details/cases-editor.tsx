import { Plus, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select.tsx";
import { cn } from "@/lib/utils.ts";
import { SettingEditor } from "@/settings/setting-editor.tsx";
import type { GraphNode } from "../node-sentence.ts";

interface CasesEditorProps {
  /** A node that takes cases: Switch. */
  node: GraphNode;
  /** The types its cases may take while it notes none, the first chosen first. */
  types: readonly string[];
  /** The node's sentence, for whom a Zone is chosen on the map. */
  who: string;
  /** An edit of its cases, saved at once; whether it was. */
  onEdit: (edit: GraphEdit_Deserialize) => Promise<boolean>;
}

/**
 * A Switch's cases (the owner, 2026-11-17, `docs/bot.md`, "The flow's kinds (v2.3)"): a row each in its type's editor,
 * × removing it with its output's wire, and a last row adding the value typed there; a case given twice marked, the
 * blueprint's problem. While the node notes no type, the cases' type is chosen first, which the first case gives it.
 * Each change sends the whole cases, built on the last ones sent, so two quick changes do not undo one another.
 */
export function CasesEditor({ node, types, who, onEdit }: CasesEditorProps) {
  const { t } = useTranslation();
  const cases = node.options?.cases ?? [];
  // The cases as they last came back, which a refusal puts back; and those last sent, which the next change builds on.
  const latest = useRef(cases);
  latest.current = cases;
  const sent = useRef(cases);
  // A case's × waits for the cases to come back, so a second click never removes the case renumbered into its place.
  const removing = useRef(false);
  const casesKey = JSON.stringify(cases);
  // biome-ignore lint/correctness/useExhaustiveDependencies: the cases as they came back, read once per change of them
  useEffect(() => {
    sent.current = latest.current;
    removing.current = false;
  }, [casesKey]);
  const typed = node.options?.valueType;
  const [chosen, setChosen] = useState(types[0] ?? "number");
  const valueType = typed ?? chosen;
  // Each change says its type: a Switch whose last case was just removed notes none on the Rust side, while the
  // graph shown still gives it the one it had.
  const answered = (kept: boolean) => {
    if (!kept) {
      sent.current = latest.current;
      removing.current = false;
    }
    return kept;
  };
  const save = (change: (cases: string[]) => string[]) => {
    const next = change(sent.current);
    sent.current = next;
    return onEdit({ edit: "options", node: node.id, cases: next, of: valueType }).then(answered);
  };
  const remove = (at: number) => {
    if (removing.current) {
      return;
    }
    removing.current = true;
    sent.current = sent.current.filter((_, index) => index !== at);
    void onEdit({ edit: "removePin", node: node.id, pin: `case_${at + 1}` }).then(answered);
  };
  const editor = { valueType, list: false, bounds: null };
  const given = (at: number) => cases.indexOf(cases[at] ?? "") < at;
  return (
    <div className="flex flex-col gap-1.5">
      {typed === undefined && (
        <Select value={chosen} onValueChange={setChosen}>
          <SelectTrigger size="sm" aria-label={t("blueprint.details.casesType")} className="h-[26px] w-full text-xs">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {types.map((one) => (
              <SelectItem key={one} value={one} className="text-xs">
                <span aria-hidden="true" className={cn("blueprint-chip-dot", `blueprint-type-${one}`)} />
                {/* A kind's types are `ValueType`'s names, each with its key under `blueprint.type`. */}
                {t(`blueprint.type.${one}` as never)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}
      {cases.map((json, at) => (
        // A case is known by its place, which its output's number is.
        // biome-ignore lint/suspicious/noArrayIndexKey: a case's place is its output's number
        <div key={at} className="flex flex-col gap-0.5">
          <div className="flex items-center gap-1">
            <div className="min-w-0 flex-1">
              <SettingEditor
                variable={editor}
                value={json}
                onChange={(changed) => save((now) => now.map((one, index) => (index === at ? changed : one)))}
                who={who}
                label={t("blueprint.details.case", { number: at + 1 })}
              />
            </div>
            <button
              type="button"
              aria-label={t("blueprint.details.caseRemove", { number: at + 1 })}
              onClick={() => remove(at)}
              className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
            >
              <X aria-hidden="true" size={14} strokeWidth={1.5} />
            </button>
          </div>
          {given(at) && <p className="text-[#f48771]">{t("blueprint.details.caseGiven")}</p>}
        </div>
      ))}
      <div className="flex items-center gap-1">
        <Plus aria-hidden="true" size={14} strokeWidth={1.5} className="text-text-muted shrink-0" />
        <div className="min-w-0 flex-1">
          <SettingEditor
            // A case added leaves an empty row for the next.
            key={`${valueType}-${cases.length}`}
            variable={editor}
            value={null}
            onChange={(added) => save((now) => [...now, added])}
            who={who}
            label={t("blueprint.details.caseAdd")}
          />
        </div>
      </div>
    </div>
  );
}
