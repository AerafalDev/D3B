import { convertFileSrc } from "@tauri-apps/api/core";
import { useState } from "react";

import { initials } from "./names.ts";

interface AvatarProps {
  name: string;
  /** The file of the breed's head the client draws for the character's sex, in the game data's folder. */
  head: string | null;
}

/**
 * A character's avatar: its breed's head, 40 px in a rounded square (the owner's choice from mock-ups, 2026-11-13), or
 * its initials in a circle while the head is unknown or cannot be loaded. The head is decorative: the breed's name is
 * written beside it.
 */
export function Avatar({ name, head }: AvatarProps) {
  const [failed, setFailed] = useState<string | null>(null);

  if (head !== null && failed !== head) {
    return (
      <img
        src={convertFileSrc(head)}
        alt=""
        onError={() => setFailed(head)}
        className="size-10 shrink-0 rounded-lg bg-[#2a2c30]"
      />
    );
  }
  return (
    <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-[#2a2c30] text-sm font-medium">
      {initials(name)}
    </span>
  );
}
