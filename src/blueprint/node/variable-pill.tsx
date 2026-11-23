import { Handle, type Node, type NodeProps, Position } from "@xyflow/react";

import { cn } from "@/lib/utils.ts";

/** What a variable's pill carries: its name and type, and whether a list or ranges. */
export type PillData = { name: string; valueType: string; list?: boolean; ranges?: boolean; faulty?: boolean };

/** A pill as React Flow holds it. */
export type PillNode = Node<PillData, "pill">;

/** A variable read in the graph, as Unreal's "Get": a pill bordered with its type's colour. */
export function VariablePill({ data, selected }: NodeProps<PillNode>) {
  return (
    <div
      className={cn(
        "blueprint-pill",
        `blueprint-type-${data.valueType}`,
        selected && "is-selected",
        data.faulty && "is-faulty",
      )}
    >
      <span className={cn("blueprint-chip-dot", `blueprint-type-${data.valueType}`)} />
      {data.name}
      <Handle
        type="source"
        position={Position.Right}
        id="value"
        className={cn(
          "blueprint-pin blueprint-value",
          `blueprint-type-${data.valueType}`,
          data.list === true && "blueprint-list",
          data.ranges === true && "blueprint-ranges",
        )}
      />
    </div>
  );
}
