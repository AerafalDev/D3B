import { useTranslation } from "react-i18next";

import type { DetailLine } from "@/bindings.ts";
import { useInspector } from "./inspector-store.ts";

/** A field read without our `.proto` files, by its number: a length-delimited one by its length alone, never its text. */
function Field({ line }: { line: Exclude<DetailLine, { kind: "decoded" }> }) {
  const { t } = useTranslation();
  switch (line.kind) {
    case "varint":
    case "fixed64":
    case "fixed32":
      return <>{t(`inspector.line.${line.kind}`, { number: line.number, value: line.value })}</>;
    case "bytes":
      return <>{t("inspector.line.bytes", { number: line.number, count: line.length })}</>;
    case "unreadable":
      return <>{t("inspector.line.unreadable")}</>;
  }
}

/**
 * The chosen frame's detail, in a column on the list's right (the owner's layout of 2026-11-13): a pinned message's
 * fields by their names, then, dimmer, the fields our `.proto` does not read by their numbers; any other frame by its
 * fields' numbers alone. Its name and size are in its row, never repeated here. A secret says its content is never
 * kept; a frame forgotten since it was listed, that it is no longer kept.
 */
export function FrameDetail() {
  const { t } = useTranslation();
  const selected = useInspector((store) => store.selected);
  const detail = useInspector((store) => store.detail);
  const secret = useInspector(
    (store) => store.frames.find((frame) => frame.number === store.selected)?.outcome === "secret",
  );

  let content = null;
  if (selected === null || detail === null) {
    content = null;
  } else if (detail.kind === "gone") {
    content = <p className="font-sans whitespace-normal">{t("inspector.gone")}</p>;
  } else if (detail.kind === "closed") {
    content = <p className="font-sans whitespace-normal">{t("inspector.nothing")}</p>;
  } else if (secret) {
    content = <p className="font-sans whitespace-normal">{t("inspector.secret")}</p>;
  } else {
    content = detail.lines.map((line, index) =>
      line.kind === "decoded" ? (
        // biome-ignore lint/suspicious/noArrayIndexKey: a frame's lines never change once answered; their order is theirs.
        <p key={index} className="text-text">
          {line.text}
        </p>
      ) : (
        // biome-ignore lint/suspicious/noArrayIndexKey: a frame's lines never change once answered; their order is theirs.
        <p key={index} className="text-text-faint">
          <Field line={line} />
        </p>
      ),
    );
  }
  return (
    <section
      aria-label={t("inspector.detail")}
      className="border-line text-text-muted w-[300px] max-w-[40%] shrink-0 overflow-auto border-l px-3 py-2 font-mono text-xs leading-[1.55] whitespace-pre"
    >
      {content}
    </section>
  );
}
