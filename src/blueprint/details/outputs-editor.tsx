import { Plus, X } from "lucide-react";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import { addedPin, pinRemoval } from "../edit/node-options.ts";
import type { GraphNode } from "../node-sentence.ts";
import { pinLabel } from "../pin-label.ts";

interface OutputsEditorProps {
  /** A node whose kind numbers its outputs: Sequence. */
  node: GraphNode;
  /** An edit of its outputs, saved at once; whether it was. */
  onEdit: (edit: GraphEdit_Deserialize) => Promise<boolean>;
}

/**
 * A Sequence's outputs in Details (the owner, 2026-11-17: the keyboard's way, as a Switch's cases): a row each, × removing
 * it with its wire while it has more than its kind's least, and "+ Add an output", as on the node.
 */
export function OutputsEditor({ node, onEdit }: OutputsEditorProps) {
  const { t } = useTranslation();
  const prefix = node.adds?.pin ?? "";
  const outputs = node.pins.filter((pin) => pin.side === "out" && new RegExp(`^${prefix}_[1-9][0-9]*$`).test(pin.id));
  const added = addedPin(node);
  // An edit waits for the outputs to come back, so a second click never removes the output renumbered into its place.
  const waiting = useRef(false);
  const count = outputs.length;
  // biome-ignore lint/correctness/useExhaustiveDependencies: the outputs as they came back, read once per change of them
  useEffect(() => {
    waiting.current = false;
  }, [count]);
  const send = (edit: GraphEdit_Deserialize) => {
    if (!waiting.current) {
      waiting.current = true;
      // A refused edit brings no outputs back: the next click is not waited for.
      void onEdit(edit).then((kept) => {
        waiting.current = waiting.current && kept;
      });
    }
  };
  return (
    <div className="flex flex-col gap-1">
      {outputs.map((pin) => {
        const removal = pinRemoval(node, pin.id);
        const label = pinLabel(pin.id, node);
        return (
          <div key={pin.id} className="flex h-[22px] items-center justify-between">
            <span>{label}</span>
            {removal !== null && (
              <button
                type="button"
                aria-label={t("blueprint.details.outputRemove", { output: label })}
                onClick={() => send(removal)}
                className="text-text-muted hover:bg-hover focus-visible:outline-focus flex size-5 shrink-0 items-center justify-center rounded hover:text-white focus-visible:outline-1"
              >
                <X aria-hidden="true" size={14} strokeWidth={1.5} />
              </button>
            )}
          </div>
        );
      })}
      {added !== null && (
        <button
          type="button"
          onClick={() => send(added)}
          className="text-text-muted hover:bg-hover focus-visible:outline-focus flex w-max items-center gap-1 rounded px-1 hover:text-white focus-visible:outline-1"
        >
          <Plus aria-hidden="true" size={14} strokeWidth={1.5} />
          {t("blueprint.addOutput")}
        </button>
      )}
    </div>
  );
}
