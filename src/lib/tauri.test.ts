import { afterEach, describe, expect, it } from "vitest";

import { isTauri } from "./tauri.ts";

describe("isTauri", () => {
  afterEach(() => {
    Reflect.deleteProperty(globalThis, "__TAURI_INTERNALS__");
  });

  it("is false in a plain browser", () => {
    expect(isTauri()).toBe(false);
  });

  it("is true inside the Tauri webview", () => {
    Reflect.set(globalThis, "__TAURI_INTERNALS__", {});
    expect(isTauri()).toBe(true);
  });
});
