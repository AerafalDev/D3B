/** The four glyphs of a window's buttons. */
export type WindowGlyphName = "minimize" | "maximize" | "restore" | "close";

interface WindowGlyphProps {
  name: WindowGlyphName;
}

/**
 * A window button's glyph drawn as Windows 11 draws its own: 10 px, a 1 px line, crisp on the pixel grid. The one
 * exception to Lucide (the owner, 2026-11-13): these buttons are the system's, not the app's.
 */
export function WindowGlyph({ name }: WindowGlyphProps) {
  return (
    <svg
      aria-hidden="true"
      data-icon={name}
      width="10"
      height="10"
      viewBox="0 0 10 10"
      fill="none"
      stroke="currentColor"
      className="text-text shrink-0"
    >
      {name === "minimize" && <path d="M0 5.5H10" />}
      {name === "maximize" && <rect x="0.5" y="0.5" width="9" height="9" />}
      {name === "restore" && (
        <>
          <rect x="0.5" y="2.5" width="7" height="7" />
          <path d="M2.5 2.5V0.5H9.5V7.5H7.5" />
        </>
      )}
      {name === "close" && <path d="M0.5 0.5L9.5 9.5M9.5 0.5L0.5 9.5" />}
    </svg>
  );
}
