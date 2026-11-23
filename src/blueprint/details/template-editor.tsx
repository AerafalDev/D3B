import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";

import type { GraphEdit_Deserialize } from "@/bindings.ts";
import type { GraphNode } from "../node-sentence.ts";

interface TemplateEditorProps {
  /** A node that takes a template: Text. */
  node: GraphNode;
  /** The template saved, whether it was. */
  onEdit: (edit: GraphEdit_Deserialize) => Promise<boolean>;
}

/**
 * A Text's template in Details (the owner, 2026-11-17: one field): a `{name}` typed there becomes an input once the
 * field is left or Enter pressed, a hole gone taking its wire with it; a template refused is put back.
 */
export function TemplateEditor({ node, onEdit }: TemplateEditorProps) {
  const { t } = useTranslation();
  const saved = node.options?.text ?? "";
  const [draft, setDraft] = useState(saved);
  useEffect(() => setDraft(saved), [saved]);
  const commit = () => {
    if (draft === saved) {
      return;
    }
    void onEdit({ edit: "text", node: node.id, text: draft }).then((kept) => {
      if (!kept) {
        setDraft(saved);
      }
    });
  };
  return (
    <div className="flex flex-col gap-1">
      <input
        type="text"
        aria-label={t("blueprint.details.template")}
        placeholder={t("blueprint.details.templateHint")}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={commit}
        onKeyDown={(event) => event.key === "Enter" && commit()}
        className="focus:border-focus h-[26px] rounded border border-[#3c3c3c] bg-[#181818] px-2 text-xs text-white outline-0"
      />
    </div>
  );
}
