import { Handle, type Node, type NodeProps, Position } from "@xyflow/react";

import { cn } from "@/lib/utils.ts";

/** What a variable's pill carries: its name and type. */
export type PillData = { name: string; valueType: string };

/** A pill as React Flow holds it. */
export type PillNode = Node<PillData, "pill">;

/** A variable read in the graph, as Unreal's "Get": a pill bordered with its type's colour. */
export function VariablePill({ data, selected }: NodeProps<PillNode>) {
  return (
    <div className={cn("blueprint-pill", `blueprint-type-${data.valueType}`, selected && "is-selected")}>
      <span className={cn("blueprint-chip-dot", `blueprint-type-${data.valueType}`)} />
      {data.name}
      <Handle
        type="source"
        position={Position.Right}
        id="value"
        className={cn("blueprint-pin blueprint-value", `blueprint-type-${data.valueType}`)}
      />
    </div>
  );
}
