import { type ConnectionLineComponentProps, getBezierPath } from "@xyflow/react";
import { use } from "react";
import { useTranslation } from "react-i18next";

import { GraphPins } from "./graph-pins.ts";
import { linkRefusal } from "./link-check.ts";

/**
 * The wire being dragged, coloured by what its pin carries; over a pin that refuses it, why, beside the pointer
 * (the owner's prototype choice: a wrong link is refused while it is dragged, with its reason).
 */
export function ConnectionLine({
  fromNode,
  fromHandle,
  fromX,
  fromY,
  fromPosition,
  toX,
  toY,
  toPosition,
  toNode,
  toHandle,
}: ConnectionLineComponentProps) {
  const { t } = useTranslation();
  const pinOf = use(GraphPins);
  const side = (type: "source" | "target") => (type === "source" ? "out" : "in");
  const from = pinOf(fromNode.id, fromHandle.id ?? null, side(fromHandle.type));
  const to = toNode === null || toHandle === null ? null : pinOf(toNode.id, toHandle.id ?? null, side(toHandle.type));
  const refusal = from !== null && to !== null ? linkRefusal(from, to) : null;
  const [path] = getBezierPath({
    sourceX: fromX,
    sourceY: fromY,
    sourcePosition: fromPosition,
    targetX: toX,
    targetY: toY,
    targetPosition: toPosition,
  });
  return (
    <g className={`blueprint-dragged blueprint-type-${from?.pin.carries ?? "exec"}`}>
      <path d={path} fill="none" className={refusal === null ? undefined : "is-refused"} />
      {refusal !== null && (
        <text x={toX + 12} y={toY - 12} className="blueprint-refusal">
          {t(`blueprint.refused.${refusal}`)}
        </text>
      )}
    </g>
  );
}
