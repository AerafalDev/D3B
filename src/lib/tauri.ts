/** Whether the page runs inside the Tauri webview, rather than in a plain browser through `bun run dev`. */
export function isTauri(): boolean {
  return "__TAURI_INTERNALS__" in globalThis;
}
