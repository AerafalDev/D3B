import { cn } from "@/lib/utils.ts";
import { type IconName, icons } from "./icons.ts";

interface IconProps {
  name: IconName;
  className?: string;
}

/**
 * A Lucide icon, decorative: the control holding it carries the label. Drawn with a 1.5 px stroke, finer than Lucide's
 * 2 px, to stay sober at 16 px; `data-icon` names it for the tests.
 */
export function Icon({ name, className }: IconProps) {
  const entry: { icon: (typeof icons)[IconName]["icon"]; tone?: string } = icons[name];
  const Drawn = entry.icon;
  return (
    <Drawn
      aria-hidden="true"
      data-icon={name}
      strokeWidth={1.5}
      className={cn("size-4 shrink-0 text-[#ced0d6]", entry.tone, className)}
    />
  );
}
