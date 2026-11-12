import { describe, expect, it } from "vitest";

import base from "../../../src-tauri/tauri.conf.json";
import windows from "../../../src-tauri/tauri.windows.conf.json";

// Tauri merges `tauri.windows.conf.json` over `tauri.conf.json` and replaces the `windows` array whole, so the
// Windows file repeats the main window's entry: this keeps the two from drifting apart.
describe("the window on Windows", () => {
  it("is the main window without its frame, keeping the shadow that rounds its corners", () => {
    expect(windows.app.windows).toEqual([{ ...base.app.windows[0], decorations: false, shadow: true }]);
  });
});
