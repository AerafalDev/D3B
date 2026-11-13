import { convertFileSrc } from "@tauri-apps/api/core";
import { useState } from "react";

import { cn } from "@/lib/utils.ts";

import { initials } from "./names.ts";

interface AvatarProps {
  name: string;
  /** The file of the breed's head the client draws for the character's sex, in the game data's folder. */
  head: string | null;
  /** 40 px in a character's tab, 28 px in the Characters list, whose selected row is the tab's grey. */
  size: "tab" | "row";
}

const LOOK = {
  tab: { box: "size-10", head: "rounded-lg bg-[#2a2c30]", initials: "bg-[#2a2c30] text-sm" },
  row: { box: "size-7", head: "rounded-md bg-[#34373d]", initials: "bg-[#34373d] text-[11px]" },
} as const;

/**
 * A character's avatar: its breed's head in a rounded square (the owner's choice from mock-ups, 2026-11-13, in its tab
 * and its row of the Characters list), or its initials in a circle while the head is unknown or cannot be loaded. The
 * head is decorative: the character's name is written beside it, and in the tab its breed's.
 */
export function Avatar({ name, head, size }: AvatarProps) {
  const look = LOOK[size];
  const [failed, setFailed] = useState<string | null>(null);

  if (head !== null && failed !== head) {
    return (
      <img
        src={convertFileSrc(head)}
        alt=""
        onError={() => setFailed(head)}
        className={cn("shrink-0", look.box, look.head)}
      />
    );
  }
  return (
    <span
      aria-hidden="true"
      className={cn("flex shrink-0 items-center justify-center rounded-full font-medium", look.box, look.initials)}
    >
      {initials(name)}
    </span>
  );
}
