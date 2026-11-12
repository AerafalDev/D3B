import { cn } from "@/lib/utils.ts";
import { type IconName, icons } from "./icons.ts";

interface IconProps {
  name: IconName;
  className?: string;
}

/**
 * A JetBrains icon, decorative: the control holding it carries the label. Drawn as an image, so it keeps the icon's
 * own colours, as in Rider.
 */
export function Icon({ name, className }: IconProps) {
  return (
    <img src={icons[name]} alt="" aria-hidden="true" draggable={false} className={cn("size-4 shrink-0", className)} />
  );
}
